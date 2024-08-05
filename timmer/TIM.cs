using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace timmer
{
    public class RECT
    {
        public ushort x, y;     /* offset point on VRAM */
        public ushort w, h;     /* width and height */
    }

    public class CLOSEST
    {
        public ushort icolor;  // Color from image
        public int ccolor;  // CLUT index
    }
    
    public class CLUT
    {
        public List<Color> colors = new List<Color>();
        public List<int[]> numbers = new List<int[]>();
    }

    public class TIM
    {
        uint mode;      /* pixel mode */
        uint csize;     /* Length of entire clut block including csize */
        RECT crect;     /* CLUT rectangle on frame buffer */
        uint ccount;    /* number of cluts */
        ushort[] cdata; /* clut data */
        List<int[]> acolors = new List<int[]>();
        List<CLOSEST> ccolors = new List<CLOSEST>();
        uint psize;     /* length of entire pixel block including psize */
        RECT prect;     /* texture image rectangle on frame buffer */
        byte[] pdata;   /* pixel data */
        uint ppos;
        uint mask;
        bool useSTP;
        List<CLUT> cluts = new List<CLUT>();



        public TIM(string aTimFilename)
        {
            BinaryReader aTIM = new BinaryReader(File.OpenRead(aTimFilename));

            ReadTIM(aTIM);

            aTIM.Close();
        }

        public TIM(BinaryReader aTIM, bool useSTP)
        {
            ReadTIM(aTIM, useSTP);
        }

        public TIM(string extractFrom, string tpos)
        {
            BinaryReader aFile = new BinaryReader(File.OpenRead(extractFrom));

            int timPos = tpos.StartsWith("0x") ? Int32.Parse(tpos.Substring(2), NumberStyles.HexNumber) : Int32.Parse(tpos);
            aFile.BaseStream.Seek(timPos, SeekOrigin.Begin);

            ReadTIM(aFile);

            aFile.Close();
        }

        public TIM(string extractFrom, uint bpp, ushort w, ushort h, string ppos, string cpos)
        {
            uint psize = 0;
            switch (bpp)
            {
                case 4:
                    bpp = 0;
                    psize = (uint)((w * h) / 2);
                    break;
                case 8:
                    bpp = 1;
                    psize = (uint)(w * h);
                    break;
                case 16:
                    bpp = 2;
                    psize = (uint)((w * h) * 2);
                    break;
                case 32:
                    bpp = 3;
                    psize = (uint)((w * h) * 3);
                    break;
            }


            BinaryReader infile = new BinaryReader(File.OpenRead(extractFrom));

            if (!String.IsNullOrEmpty(cpos))
            {
                ushort[] cdata = new ushort[bpp == 0 ? 16 : 256];
                if (bpp == 0 || bpp == 1)
                {
                    int clutPos = cpos.StartsWith("0x") ? Int32.Parse(cpos.Substring(2), NumberStyles.HexNumber) : Int32.Parse(cpos);

                    infile.BaseStream.Seek(clutPos, SeekOrigin.Begin);

                    for (int i = 0; i < cdata.Length; i++)
                    {
                        cdata[i] = infile.ReadUInt16();
                    }

                    for (int i = 0; i < cdata.Length; i++)
                    {
                        this.acolors.Add(ColorToArray(RGBAToColor(cdata[i])));
                    }
                }
                this.cdata = cdata;
            }

            int pixelPos = ppos.StartsWith("0x") ? Int32.Parse(ppos.Substring(2), NumberStyles.HexNumber) : Int32.Parse(ppos);
            infile.BaseStream.Seek(pixelPos, SeekOrigin.Begin);
            byte[] pdata = infile.ReadBytes((int)psize);

            infile.Close();

            this.mode = bpp;
            this.ccount = 1;
            this.prect = new RECT()
            {
                w = w,
                h = h
            };
            
            this.pdata = pdata;


        }

        public TIM(uint mode, uint ccount, ushort w, ushort h, ushort[] cdata, byte[] pdata)
        {
            this.mode = mode;
            this.ccount = ccount;
            this.prect = new RECT()
            {
                w = w,
                h = h
            };

            this.cdata = cdata;
            this.pdata = pdata;

            acolors = new List<int[]>();
            for (int i = 0; i < cdata.Length; i++)
            {
                acolors.Add(ColorToArray(RGBAToColor(cdata[i])));
            }
        }

        void ReadTIM(BinaryReader aTim, bool useSTP = true)
        {
            this.useSTP = useSTP;

            uint magicId = aTim.ReadUInt32();
            if (magicId != 0x0010)
            {
                throw new Exception("Not a valid TIM!");
            }

            /* Bits 0 - 2 (PMODE)
             * 0 : 4-bit CLUT
             * 1 : 8-Bit CLUT 
             * 2 : 15 bit direct 
             * 3 : 24-bit direct
             * 4 : Mixed 
             * 
             * Bits 3 (CF)
             * 0 : No CLUT section
             * 1 : Has CLUT Section             
             */
            mode = aTim.ReadUInt32();
            if ((mode & 7) > 4)
                throw new Exception(("Mode is invalid!"));

            if ((mode & 8) > 0)
            {
                csize = aTim.ReadUInt32();
                crect = new RECT()
                {
                    x = aTim.ReadUInt16(),
                    y = aTim.ReadUInt16(),
                    w = aTim.ReadUInt16(),
                    h = aTim.ReadUInt16()
                };

                ccount = 0;
                // CLUT entries are 16 bits (2 bytes) each
                ccount = ((mode & 7) == 1) ? ((csize - 12) / 2) / 256 : ((csize - 12) / 2) / 16;
                // Number of colors in the CLUT
                int numColors = ((mode & 7) == 1) ? 256 : 16;

                if ((mode & 7) == 1 && (((csize - 12) / 2) % 256 != 0))
                {
                    throw new Exception("Invalid CLUT size!");
                }

                if ((mode & 7) == 0 && (((csize - 12) / 2) % 16 != 0))
                {
                    throw new Exception("Invalid CLUT size!");
                }

                cdata = new ushort[((int)csize - 12) / 2];
                for (int i = 0; i < cdata.Length; i++)
                {
                    cdata[i] = aTim.ReadUInt16();
                    acolors.Add(ColorToArray(RGBAToColor(cdata[i])));
                }

                cluts = new List<CLUT>();


                CLUT clut = new CLUT();
                for (int i = 0; i < cdata.Length; i++)
                {
                    clut.colors.Add(RGBAToColor(cdata[i]));
                    clut.numbers.Add(ColorToArray(RGBAToColor(cdata[i])));


                    if (clut.colors.Count == numColors)
                    {
                        cluts.Add(clut);
                        clut = new CLUT();
                    }
                }
            }

            psize = aTim.ReadUInt32();

            prect = new RECT()
            {
                x = aTim.ReadUInt16(),
                y = aTim.ReadUInt16(),
                w = aTim.ReadUInt16(),
                h = aTim.ReadUInt16()
            };

            uint truepsize = 0;
            if ((mode & 7) == 0) // 4 bit
            {
                prect.w = (ushort)(prect.w * 4);
                //So apparently psize can be wrong.... Looking at you fox junction!
                truepsize = (uint)((prect.w * prect.h) / 2);
            }
            else if ((mode & 7) == 1) // 8 bit
            {
                prect.w = (ushort)(prect.w * 2);
                truepsize = (uint)(prect.w * prect.h);
            }
            else if ((mode & 7) == 2) // 16 bit
            {
                truepsize = (uint)((prect.w * prect.h) * 2);
            }
            else if ((mode & 7) == 3) // 24 bit
            {
                prect.w = (ushort)(prect.w / 1.5); // WHY?
                truepsize = (uint)((prect.w * prect.h) * 3);
            }

            ppos = (uint)aTim.BaseStream.Position;
            pdata = aTim.ReadBytes((int)truepsize);
        }

        public uint GetPixelPos()
        {
            return ppos;
        }
        public byte[] GetPixelData()
        {
            return pdata;
        }

        int GetIndexOfTransparentColor()
        {
            for (int i = 0; i < acolors.Count; i++)
            {
                if (acolors[i][3] == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        public void SetMask(uint mask)
        {
            this.mask = mask;
        }

        // Thanks Hilltop for helping me with code!
        int GetClosestColor(int[] targetColor, int clutIdx)
        {
            for (int i = 0; i < ccolors.Count; i++)
            {
                if (ccolors[i].icolor == ColorToRGBA(ArrayToColor(targetColor)))
                {
                    return ccolors[i].ccolor;
                }
            }

            int closestColorIdx = cluts[clutIdx].numbers.Count - 1;
            if (targetColor[3] != 0)
            {
                double minDistance = GetRGBDistance(cluts[clutIdx].numbers[cluts[clutIdx].numbers.Count - 1], targetColor);

                for (int i = 0; i < cluts[clutIdx].numbers.Count; i++)
                {
                    double distance = GetRGBDistance(cluts[clutIdx].numbers[i], targetColor);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestColorIdx = i;
                    }
                }
            }
            else
            {
                closestColorIdx = GetIndexOfTransparentColor();
            }

            ccolors.Add(new CLOSEST()
            {
                icolor = ColorToRGBA(ArrayToColor(targetColor)),
                ccolor = closestColorIdx
            });

            return closestColorIdx;
        }

        double GetRGBDistance(int[] color1, int[] color2)
        {
            double sumSquared = 0;
            for (int i = 0; i < 3; i++)
            {
                double diff = color1[i] - color2[i];
                sumSquared += diff * diff;
            }
            return Math.Sqrt(sumSquared);
        }

        int GetAlphaFromSTP(int c)
        {
            // I'm just gonna go with translucent processing on ;_;
            /* STP/R,G,B    Translucent processing on   Translucent processing off
             * 0/0,0,0 T    Transparent                 Transparent
             * 0/X,X,X      Not transparent             Not transparent
             * 1/X,X,X      Semi-transparent            Not transparent
             * 1/0,0,0      Non-transparent black       Non-transparent black
             */

            int stp = ((c & 0x8000) >> 15);

            int a = 255;
            if (useSTP)
            {
                if (stp == 0 && (c & 0x7FFF) == 0)
                {
                    a = 0;
                }
            }
            return a;
        }


        int[] ColorToArray(Color c)
        {
            return new int[] { c.R, c.G, c.B, c.A };
        }

        Color ArrayToColor(int[] c)
        {
            return Color.FromArgb(c[3], c[0], c[1], c[2]);
        }

        Color RGBAToColor(int c)
        {
            int r = (c & 0x1f) << 3;
            int g = ((c & 0x3e0) >> 5) << 3;
            int b = ((c & 0x7C00) >> 10) << 3;
            int a = GetAlphaFromSTP(c);

            return Color.FromArgb(a, r, g, b);
        }

        ushort ColorToRGBA(Color color)
        {
            int r = color.R >> 3;
            int g = color.G >> 3;
            int b = color.B >> 3;
            int a = color.A;

            ushort c = (ushort)r;
            c |= (ushort)(g << 5);
            c |= (ushort)(b << 10);

            if (a != 255)
            {
                c |= (ushort)(1 << 15);
            }

            return c;
        }

        public Bitmap[] Export4Bpp()
        {
            List<Bitmap> images = new List<Bitmap>();
            for (int i = 0; i < cluts.Count; i++)
            {
                Bitmap image = new Bitmap(prect.w, prect.h);
                int pidx = 0;
                byte pixel = 0;
                for (int y = 0; y < prect.h; y++)
                {
                    for (int x = 0; x < prect.w; x += 2)
                    {
                        pixel = pdata[pidx++];
                        //ushort c = //cdata[(i * 16) + (pixel & 0x0F)];
                        image.SetPixel(x, y, cluts[i].colors[pixel & 0x0F]);

                        //c = cdata[(i * 16) + (pixel >> 4)];
                        image.SetPixel(x + 1, y, cluts[i].colors[pixel >> 4]);
                    }
                }
                images.Add(image);
            }
            return images.ToArray();
        }

        public void Import4Bpp(Bitmap image, int clutIdx)
        {
            int num = 0;
            uint num2 = mask;
            for (int i = 0; i < prect.h; i++)
            {
                for (int j = 0; j < prect.w; j += 2)
                {
                    byte b = pdata[num];
                    b = (byte)((b & (num2 << 4)) | (b & num2));
                    b |= (byte)((uint)GetClosestColor(ColorToArray(image.GetPixel(j, i)), clutIdx) & 0xF);
                    b |= (byte)((GetClosestColor(ColorToArray(image.GetPixel(j + 1, i)), clutIdx) & 0xF) << 4);
                    pdata[num++] = b;
                }
            }
        }

        public Bitmap[] Export8Bpp()
        {
            List<Bitmap> images = new List<Bitmap>();
            for (int i = 0; i < cluts.Count; i++)
            {
                Bitmap image = new Bitmap(prect.w, prect.h);
                int pidx = 0;
                for (int y = 0; y < prect.h; y++)
                {
                    for (int x = 0; x < prect.w; x++)
                    {
                        byte pixel = pdata[pidx++];
                        //ushort c = cdata[(i * 256) + pixel];
                        image.SetPixel(x, y, cluts[i].colors[pixel]);
                    }
                }
                images.Add(image);
            }
            return images.ToArray();
        }

        public void Import8Bpp(Bitmap image, int clutIdx)
        {
            Bitmap toCompareTo = Export8Bpp()[clutIdx];

            int pidx = 0;
            for (int y = 0; y < prect.h; y++)
            {
                for (int x = 0; x < prect.w; x++)
                {
                    Color c1 = toCompareTo.GetPixel(x, y);
                    Color c2 = image.GetPixel(x, y);
                    if (!(c1.R == c2.R && c1.G == c2.G && c1.B == c2.B))
                    {
                        pdata[pidx] = (byte)GetClosestColor(ColorToArray(c2), clutIdx);
                    }
                    pidx++;
                }
            }
        }

        public Bitmap[] Export16Bpp()
        {
            Bitmap image = new Bitmap(prect.w, prect.h);

            int pidx = 0;
            for (int y = 0; y < prect.h; y++)
            {
                for (int x = 0; x < prect.w; x++)
                {
                    ushort pixel = pdata[pidx++];
                    pixel |= (ushort)(pdata[pidx++] << 8);
                    image.SetPixel(x, y, RGBAToColor(pixel));
                }
            }

            return new Bitmap[] { image };
        }

        public void Import16Bpp(Bitmap image)
        {
            int pidx = 0;
            for (int y = 0; y < prect.h; y++)
            {
                for (int x = 0; x < prect.w; x++)
                {
                    ushort pixel = ColorToRGBA(image.GetPixel(x, y));
                    pdata[pidx++] = (byte)(pixel & 0xFF);
                    pdata[pidx++] = (byte)(pixel >> 8);
                }
            }
        }

        public Bitmap[] Export24Bpp()
        {
            Bitmap image = new Bitmap(prect.w, prect.h);

            int pidx = 0;
            for (int y = 0; y < prect.h; y++)
            {
                for (int x = 0; x < prect.w; x++)
                {
                    byte r = pdata[pidx++];
                    byte g = pdata[pidx++];
                    byte b = pdata[pidx++];
                    image.SetPixel(x, y, Color.FromArgb(r, g, b));
                }
            }

            return new Bitmap[] { image };
        }

        public void ExportPNG(string filename)
        {
            Bitmap[] images = null;
            if ((mode & 7) == 0)        // 4 bpp
            {
                images = Export4Bpp();
            }
            else if ((mode & 7) == 1)   // 8 bpp
            {
                images = Export8Bpp();
            }
            else if ((mode & 7) == 2)   // 16 bpp
            {
                images = Export16Bpp();
            }
            else if ((mode & 7) == 3)   // 24 bpp
            {
                images = Export24Bpp();
            }
            else if ((mode & 7) == 4)   // Mixed (what is this?)
            {
                throw new Exception("Mixed not implmented!");
            }

            if (images.Length == 1)
            {
                images[0].Save(filename.Replace(".png", "_" + 0.ToString("D4") + ".png"), ImageFormat.Png);
            }
            else
            {
                for (int i = 0; i < images.Length; i++)
                {
                    images[i].Save(filename.Replace(".png", "_" + i.ToString("D4") + ".png"), ImageFormat.Png);
                }
            }
        }

        public void ImportImage(string filename)
        {
            if ((mode & 7) == 0)        // 4 bpp
            {
                int clutIdx = GetCLUTIndex(filename);
                Import4Bpp(new Bitmap(filename), clutIdx);
            }
            else if ((mode & 7) == 1)   // 8 bpp
            {
                int clutIdx = GetCLUTIndex(filename);
                Import8Bpp(new Bitmap(filename), clutIdx);
            }
            else if ((mode & 7) == 2)   // 16 bpp
            {
                Import16Bpp(new Bitmap(filename));
            }
            else if ((mode & 7) == 3)   // 24 bpp
            {
                throw new Exception("24bpp not implemented!");
            }
            else if ((mode & 7) == 4)   // Mixed (what is this?)
            {
                throw new Exception("Mixed not implmented!");
            }
        }

        public int GetCLUTIndex(string filename)
        {
            int start = filename.LastIndexOf("_") + 1;
            int end = filename.LastIndexOf(".");

            int clutIdx = Int32.Parse(filename.Substring(start, end - start));

            return clutIdx;

        }

        public void UseSTP(bool useSTP)
        {
            this.useSTP = useSTP;
        }
    }
}