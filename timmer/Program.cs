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
                uint bpp = 0;
                switch (opts.BPP)
                {
                    case 4:
                        bpp = 0;
                        break;
                    case 8:
                        bpp = 1;
                        break;
                    case 16:
                        bpp = 2;
                        break;
                    case 32:
                        bpp = 3;
                        break;
                }

                int pixelPos = opts.PixelPos.StartsWith("0x") ? Int32.Parse(opts.PixelPos.Substring(2), NumberStyles.HexNumber) : Int32.Parse(opts.PixelPos);
                int clutPos = opts.PalettePos.StartsWith("0x") ? Int32.Parse(opts.PalettePos.Substring(2), NumberStyles.HexNumber) : Int32.Parse(opts.PalettePos);

                BinaryReader infile = new BinaryReader(File.OpenRead(opts.Infilename));


                infile.BaseStream.Seek(clutPos, SeekOrigin.Begin);
                ushort[] cdata = new ushort[bpp == 4 ? 16 : 256];
                for (int i = 0; i < cdata.Length; i++)
                {
                    cdata[i] = infile.ReadUInt16();
                }

                infile.BaseStream.Seek(pixelPos, SeekOrigin.Begin);
                ushort w =  opts.Width;
                ushort h = opts.Height;
                uint psize = 0;
                if (bpp == 0) // 4 bit
                {
                    psize = (uint)((w * h) / 2);
                }
                else if (bpp == 1) // 8 bit
                {
                    psize = (uint)(w * h);
                }
                else if (bpp == 2) // 16 bit
                {
                    psize = (uint)((w * h) * 2);
                }
                else if (bpp == 3) // 24 bit
                {
                    psize = (uint)((w * h) * 3);
                }

                byte[] pdata = infile.ReadBytes((int)psize);

                TIM tim = new TIM(bpp, 1, w, h, cdata, pdata);
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
