using CommandLine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace timmer
{


    class Program
    {
        [Verb("extract", HelpText = "Extract graphic.")]
        internal class ExtractOptions
        {
            [Option('i', "infile", Required = false, HelpText = "Filename to extract graphic from.")]
            public string Infilename { get; set; }

            [Option('o', "outfile", Required = false, HelpText = "Filename to save graphic to (only PNGs are supported).")]
            public string Outfilename { get; set; }

            [Option('p', "pixeldata", Required = false, HelpText = "Position of pixel data.")]
            public string PixelPos { get; set; }
            
            [Option('c', "clutdata", Required = false, HelpText = "Position of clut/palette data.")]
            public string PalettePos { get; set; }

            [Option('b', "bpp", Required = false, HelpText = "Bits per pixel.")]
            public uint BPP { get; set; }

            [Option('w', "width", Required = false, HelpText = "Width of image.")]
            public ushort Width { get; set; }

            [Option('h', "height", Required = false, HelpText = "Height of image.")]
            public ushort Height { get; set; }


        }

        static void Main(string[] args)
        {
            var types = LoadVerbs();
            object parsed = Parser.Default.ParseArguments(args, types).WithParsed(Run);
        }

        private static void Run(object obj)
        {
            switch (obj)
            {
                case ExtractOptions e:
                    Extract(e);
                    break;
            }

            void Extract(ExtractOptions opts)
            {             
                TIM tim = new TIM(opts.Infilename, opts.BPP, opts.Width, opts.Height, opts.PixelPos, opts.PalettePos);
                tim.ExportPNG(opts.Outfilename);
            }
        }

        //load all Verb types using Reflection
        static Type[] LoadVerbs()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
        }
    }

}
