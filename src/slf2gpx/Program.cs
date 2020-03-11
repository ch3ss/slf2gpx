using CommandLine;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace slf2gpx
{
  public class Options
  {
    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }

    [Option('i', "input", Required = true, HelpText = "Sets the input file path.")]
    public string InputFilePath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Sets the output file path.")]
    public string OutputFilePath { get; set; }

  }
  class Program
  {

    static void Main(string[] args)
    {
      Parser.Default.ParseArguments<Options>(args)
       .WithParsed(o => MainImpl(o));
    }
    static void MainImpl(Options options)
    {
      if (string.IsNullOrWhiteSpace(options.InputFilePath))
        return;

      FileAttributes attr = File.GetAttributes(options.InputFilePath);
      //detect whether its a directory or file
      if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
      {
        var output = options.OutputFilePath ?? options.InputFilePath;

        var files = Directory.GetFiles(options.InputFilePath, "*.slf");

        foreach (var inputFile in files)
        {
          Process(inputFile, Path.Combine(output, $"{ Path.GetFileNameWithoutExtension(inputFile)}.gpx"));
        }

      }
      else
      {
        options.OutputFilePath = options.OutputFilePath ?? Path.ChangeExtension(options.InputFilePath, ".gpx");
        Process(options.InputFilePath, options.OutputFilePath);
      }

    }

    static void Process(string inputPath, string outputPath)
    {

      var slf = Utils.Read<Activity>(inputPath, Utils.FileType.slf);
      if (slf == null) return;
      var gpx = Slf2GpxConverter.ToGpx(slf, Path.GetFileNameWithoutExtension(inputPath));
      if (gpx == null) return;
      Utils.Write(gpx, outputPath);
    }
  }

  internal static class Utils
  {
    public enum FileType
    {
      slf,
      gpx
    }

    public static bool GetXsdSchema(FileType fileType, out XmlSchema xmlSchema)
      => GetXsdSchema($"slf2gpx.Data.{fileType}.xsd", out xmlSchema);

    public static bool GetXsdSchema(string path, out XmlSchema xmlSchema)
    {
      xmlSchema = null;
      var assembly = System.Reflection.Assembly.GetExecutingAssembly();
      using (var stream = assembly.GetManifestResourceStream(path))
      {
        if (stream == null) return false;

        xmlSchema = XmlSchema.Read(stream, null);
      }
      return true;
    }

    public static T Read<T>(string filePath, FileType type)
       where T : class
    {
      try
      {
        XmlReaderSettings settings = new XmlReaderSettings();

        if (GetXsdSchema(type, out var xmlSchema))
        {
          settings.Schemas.Add(xmlSchema);
          settings.ValidationType = ValidationType.Schema;
        }
        settings.CloseInput = true;
        var xmlReader = XmlReader.Create(filePath, settings);
        var serializer = new XmlSerializer(typeof(T));
        var gpx = serializer.Deserialize(xmlReader) as T;
        return gpx;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine(ex);
      }
      return null;
    }

    public static void Write<T>(T obj, string filePath)
      where T : class
    {
      try { 
      XmlWriterSettings settings = new XmlWriterSettings();
      settings.Indent = true;
      var xmlWriter = XmlWriter.Create(filePath, settings);
      var serializer = new XmlSerializer(typeof(T));
      serializer.Serialize(xmlWriter, obj);
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine(ex);
      }
    }
  }

  internal class Slf2GpxConverter
  {
    public static gpxType ToGpx(Activity activity, string name)
    {
      return new gpxType()
      {
        metadata = Convert(activity.GeneralInformation, name),
        trk = Convert(activity, name)
      };
    }

    private static trkType[] Convert(Activity activity, string name)
    {
      return new trkType[]
      {
        new trkType
        {
          name = name,
          trkseg = Convert(activity.Entries)
        }
      };
    }

    private static trksegType[] Convert(ActivityEntry[] entries)
    {
      return new trksegType[]
      {
        new trksegType
        {
          trkpt = entries.Select(e => Convert(e)).ToArray()
        }
      };
    }

    private static wptType Convert(ActivityEntry e)
    {
      return new wptType
      {
        lat = (decimal)e.latitude,
        lon = (decimal)e.longitude,
        ele = (decimal)e.altitude,
      };
    }
    public static metadataType Convert(ActivityGeneralInformation generalInformation, string name)
    {
      return new metadataType()
      {
        name = name,
        author = new personType
        {
          name = "slf2gpx",
          email = new emailType
          {
            domain = "christoph.hess@live.de",
          }
        }
      };
    }
  }
}
