using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Collections;

namespace BilateralFilter
{
    public unsafe partial class FrmTest : Form
    {
        static class Program
        {
            /// <summary>
            /// 应用程序的主入口点。
            /// </summary>
            [STAThread]
            static void Main()
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FrmTest());
            }
        }
        private Bitmap SrcBmp;
        private Bitmap DestBmp;
        private Bitmap HistBmp;
        private Bitmap SmoothHistBmp;
        private int[] HistGram = new int[256];
        private int[] HistGramS = new int[256];
        private List<Coord>[] coord_HistGram = new List<Coord>[256];
        private List<Coord>[] coord_HistGramSmooth = new List<Coord>[256];
        private ArrayList vecHistNumS = new ArrayList();
        private float[] avgNumPerPostion = new float[256];
        private int Thr;
        private int n_mutlpyY = 0;
        private int[] mutlpyY = new int[10];
        bool Init = false;
        public FrmTest()
        {
            InitializeComponent();
        }


        private void CmdOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Bitmap Temp = (Bitmap)Bitmap.FromFile(openFileDialog.FileName);
                if (IsGrayBitmap(Temp) == true)
                    SrcBmp = Temp;
                else
                {
                    SrcBmp = ConvertToGrayBitmap(Temp);
                    Temp.Dispose();
                }
                DestBmp = CreateGrayBitmap(SrcBmp.Width, SrcBmp.Height);
                GetHistGram(SrcBmp, HistGram);
                SrcPic.Image = SrcBmp;
                DestPic.Image = DestBmp;
                Update();
              
            }
            openFileDialog.Dispose();
        }


        private void FrmTest_Load(object sender, EventArgs e)
        {

            CmbMethod.Items.Add("灰度平均值");
            CmbMethod.Items.Add("黄式模糊阈值");
            CmbMethod.Items.Add("谷底最小值");
            CmbMethod.Items.Add("位置相关谷底最小值");
            CmbMethod.Items.Add("双峰平均值");
            CmbMethod.Items.Add("百分比阈值");
            CmbMethod.Items.Add("迭代阈值法");
            CmbMethod.Items.Add("大津法");
            CmbMethod.Items.Add("一维最大熵");
            CmbMethod.Items.Add("动能保持");
            CmbMethod.Items.Add("Kittler最小错误");
            CmbMethod.Items.Add("ISODATA法");
            CmbMethod.Items.Add("Shanbhag法");
            CmbMethod.Items.Add("Yen法"); 
            CmbMethod.SelectedIndex = 2;
            SrcBmp = global::Binaryzation.Properties.Resources.Lena;
            DestBmp = CreateGrayBitmap(SrcBmp.Width, SrcBmp.Height);
            GetHistGram(SrcBmp, HistGram);
            SrcPic.Image = SrcBmp;
            DestPic.Image = DestBmp;
            HistBmp = CreateGrayBitmap(256, 100);
            SmoothHistBmp = CreateGrayBitmap(256, 100);
            PicHist.Image = HistBmp;
            PicSmoothHist.Image = SmoothHistBmp;
            Update();
            Init = true;
        }

        private Bitmap CreateGrayBitmap(int Width, int Height)
        {
            Bitmap Bmp = new Bitmap(Width, Height, PixelFormat.Format8bppIndexed);
            ColorPalette Pal = Bmp.Palette;
            for (int Y = 0; Y < Pal.Entries.Length; Y++) Pal.Entries[Y] = Color.FromArgb(255, Y, Y, Y);
            Bmp.Palette = Pal;
            return Bmp;
        }

        private bool IsGrayBitmap(Bitmap Bmp)
        {
            if (Bmp.PixelFormat != PixelFormat.Format8bppIndexed) return false;
            if (Bmp.Palette.Entries.Length != 256) return false;
            for (int Y = 0; Y < Bmp.Palette.Entries.Length; Y++)
                if (Bmp.Palette.Entries[Y] != Color.FromArgb(255, Y, Y, Y)) return false;
            return true;
        }

        private Bitmap ConvertToGrayBitmap(Bitmap Src)
        {
            Bitmap Dest = CreateGrayBitmap(Src.Width, Src.Height);
            BitmapData SrcData = Src.LockBits(new Rectangle(0, 0, Src.Width, Src.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            BitmapData DestData = Dest.LockBits(new Rectangle(0, 0, Dest.Width, Dest.Height), ImageLockMode.ReadWrite, Dest.PixelFormat);
            int Width = SrcData.Width, Height = SrcData.Height;
            int SrcStride = SrcData.Stride, DestStride = DestData.Stride;
            byte* SrcP, DestP;
            for (int Y = 0; Y < Height; Y++)
            {
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride;         // 必须在某个地方开启unsafe功能，其实C#中的unsafe很safe，搞的好吓人。            
                DestP = (byte*)DestData.Scan0 + Y * DestStride;
                for (int X = 0; X < Width; X++)
                {
                    *DestP = (byte)((*SrcP + (*(SrcP + 1) << 1) + *(SrcP + 2)) >> 2);
                    SrcP += 3;
                    DestP++;
                }
            }
            Src.UnlockBits(SrcData);
            Dest.UnlockBits(DestData);
            return Dest;
        }

        private void GetHistGram(Bitmap Src, int[] HistGram)
        {
            BitmapData SrcData = Src.LockBits(new Rectangle(0, 0, Src.Width, Src.Height), ImageLockMode.ReadWrite, Src.PixelFormat);
            int Width = SrcData.Width, Height = SrcData.Height, SrcStride = SrcData.Stride;
            byte* SrcP;
            for (int Y = 0; Y < 256; Y++) HistGram[Y] = 0;
            for (int Y = 0; Y < Height; Y++)
            {
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride;
                for (int X = 0; X < Width; X++, SrcP++)
                {
                    if ((*SrcP) > 25 && (*SrcP) < 230) //原来是230避免太远的像素点不准确
                    {
                        HistGram[*SrcP]++;
                    }
                }
            }
            /*
            for (int Y = 0; Y < 256; Y++)
            {
                Console.WriteLine("Y" + Y + "HistGram[Y]" + HistGram[Y]);
            }
            */
            Src.UnlockBits(SrcData);
        }
        private void GetHistGramWithCoordinate(Bitmap Src, List<Coord>[] coord_HistGram)
        {
            BitmapData SrcData = Src.LockBits(new Rectangle(0, 0, Src.Width, Src.Height), ImageLockMode.ReadWrite, Src.PixelFormat);
            int Width = SrcData.Width, Height = SrcData.Height, SrcStride = SrcData.Stride;
            byte* SrcP;
            for (int Y = 0; Y < 256; Y++)
            {
                coord_HistGram[Y] = new List<Coord>();
            }
            for (int Y = 0; Y < 256; Y++)
            {
                coord_HistGramSmooth[Y] = new List<Coord>();
            }
            for (int Y = 0; Y < Height; Y++)
            {
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride;
                for (int X = 0; X < Width; X++, SrcP++)
                {
                    if ((*SrcP) > 25 && (*SrcP) < 230) //原来是230避免太远的像素点不准确
                    {
                        Coord coord = new Coord();
                        coord.setX(X);
                        coord.setY(Y);
                        //Console.WriteLine("*SrcP" + *SrcP + "HistGram[*SrcP]" + HistGram[*SrcP] + "X=" + coord.getX() + "Y=" + coord.getY() );
                        coord_HistGram[*SrcP].Add(coord);
                    }
                }
            }
            /*
            for (int Y = 0; Y < 256; Y++)
            {
                List<Coord> tmpCoord = coord_HistGram[Y];
                Console.WriteLine("Y" + Y + "tmpCoord_num" + tmpCoord.Count);
            }
             */
            Src.UnlockBits(SrcData);
        }

        private void DoBinaryzation(Bitmap Src, Bitmap Dest, int Threshold)
        {
            if (Threshold == -1)
            {
                MessageBox.Show("选择了非法的阈值变量.");
                return;
            }
            BitmapData SrcData = Src.LockBits(new Rectangle(0, 0, Src.Width, Src.Height), ImageLockMode.ReadWrite, Src.PixelFormat);
            BitmapData DestData = Dest.LockBits(new Rectangle(0, 0, Dest.Width, Dest.Height), ImageLockMode.ReadWrite, Dest.PixelFormat);
            int Width = SrcData.Width, Height = SrcData.Height;
            int SrcStride = SrcData.Stride, DestStride = DestData.Stride;
            byte* SrcP, DestP;
            for (int Y = 0; Y < Height; Y++)
            {
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride;         // 必须在某个地方开启unsafe功能，其实C#中的unsafe很safe，搞的好吓人。            
                DestP = (byte*)DestData.Scan0 + Y * DestStride;
                for (int X = 0; X < Width; X++, SrcP++, DestP++)
                    if (*SrcP == 0)
                    {
                        *DestP = byte.MinValue;
                    }
                    else
                    {
                        *DestP = *SrcP <= Threshold ? byte.MaxValue : byte.MinValue;     // 写成255和0，C#编译器不认。
                    }
            }
            Src.UnlockBits(SrcData);
            Dest.UnlockBits(DestData);
            DestPic.Invalidate();
            LblThreshold.Text = Threshold.ToString();
        }
        private void DoBinaryzationLessThr(Bitmap Src, Bitmap Dest, int Threshold)
        {
            if (Threshold == -1)
            {
                MessageBox.Show("选择了非法的阈值变量.");
                return;
            }
            BitmapData SrcData = Src.LockBits(new Rectangle(0, 0, Src.Width, Src.Height), ImageLockMode.ReadWrite, Src.PixelFormat);
            BitmapData DestData = Dest.LockBits(new Rectangle(0, 0, Dest.Width, Dest.Height), ImageLockMode.ReadWrite, Dest.PixelFormat);
            int Width = SrcData.Width, Height = SrcData.Height;
            int SrcStride = SrcData.Stride, DestStride = DestData.Stride;
            byte* SrcP, DestP;
            for (int Y = 0; Y < Height; Y++)
            {
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride;         // 必须在某个地方开启unsafe功能，其实C#中的unsafe很safe，搞的好吓人。            
                DestP = (byte*)DestData.Scan0 + Y * DestStride;
                for (int X = 0; X < Width; X++, SrcP++, DestP++)
                    if (*SrcP == 0)
                    {
                        *DestP = byte.MinValue;
                    }
                    else
                    {
                        *DestP = *SrcP <= Threshold ? byte.MaxValue : byte.MinValue;     // 写成255和0，C#编译器不认。
                    }
            }
            Src.UnlockBits(SrcData);
            Dest.UnlockBits(DestData);
            DestPic.Invalidate();
            LblThreshold.Text = Threshold.ToString();
        }
        private void DoBinaryzationLargeThr(Bitmap Src, Bitmap Dest, int Threshold)
        {
            if (Threshold == -1)
            {
                MessageBox.Show("选择了非法的阈值变量.");
                return;
            }
            BitmapData SrcData = Src.LockBits(new Rectangle(0, 0, Src.Width, Src.Height), ImageLockMode.ReadWrite, Src.PixelFormat);
            BitmapData DestData = Dest.LockBits(new Rectangle(0, 0, Dest.Width, Dest.Height), ImageLockMode.ReadWrite, Dest.PixelFormat);
            int Width = SrcData.Width, Height = SrcData.Height;
            int SrcStride = SrcData.Stride, DestStride = DestData.Stride;
            byte* SrcP, DestP;
            for (int Y = 0; Y < Height; Y++)
            {
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride;         // 必须在某个地方开启unsafe功能，其实C#中的unsafe很safe，搞的好吓人。            
                DestP = (byte*)DestData.Scan0 + Y * DestStride;
                for (int X = 0; X < Width; X++, SrcP++, DestP++)
                    if (*SrcP == 0)
                    {
                        *DestP = byte.MinValue;
                    }
                    else
                    {
                        *DestP = *SrcP >= Threshold ? byte.MaxValue : byte.MinValue;     // 写成255和0，C#编译器不认。
                    }
            }
            Src.UnlockBits(SrcData);
            Dest.UnlockBits(DestData);
            DestPic.Invalidate();
            LblThreshold.Text = Threshold.ToString();
        }
        private void DoBinaryzationBetweenTwoThrs(Bitmap Src, Bitmap Dest, int left, int right)
        {
            //Console.WriteLine("left=" + left + " right=" + right);
            if (left == -1 || right == -1)
            {
                MessageBox.Show("选择了非法的阈值变量.");
                return;
            }
            BitmapData SrcData = Src.LockBits(new Rectangle(0, 0, Src.Width, Src.Height), ImageLockMode.ReadWrite, Src.PixelFormat);
            BitmapData DestData = Dest.LockBits(new Rectangle(0, 0, Dest.Width, Dest.Height), ImageLockMode.ReadWrite, Dest.PixelFormat);
            int Width = SrcData.Width, Height = SrcData.Height;
            int SrcStride = SrcData.Stride, DestStride = DestData.Stride;
            byte* SrcP, DestP;
            for (int Y = 0; Y < Height; Y++)
            {
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride;         // 必须在某个地方开启unsafe功能，其实C#中的unsafe很safe，搞的好吓人。            
                DestP = (byte*)DestData.Scan0 + Y * DestStride;
                for (int X = 0; X < Width; X++, SrcP++, DestP++)
                    if (*SrcP == 0)
                    {
                        *DestP = byte.MinValue;
                    }
                    else
                    {
                        if (*SrcP >= left && *SrcP <= right)
                        {
                            *DestP = byte.MaxValue;     // 写成255和0，C#编译器不认。
                        }
                        else
                        {
                            *DestP = byte.MinValue;
                        }
                    }
            }
            Src.UnlockBits(SrcData);
            Dest.UnlockBits(DestData);
            DestPic.Invalidate();
            LblThreshold.Text = left.ToString();
        }
        private void DoParts(Bitmap Src, Bitmap Dest, List<Coord>[] coord_HistGramInRegions)
        {
            BitmapData SrcData = Src.LockBits(new Rectangle(0, 0, Src.Width, Src.Height), ImageLockMode.ReadWrite, Src.PixelFormat);
            BitmapData DestData = Dest.LockBits(new Rectangle(0, 0, Dest.Width, Dest.Height), ImageLockMode.ReadWrite, Dest.PixelFormat);
            int Width = SrcData.Width, Height = SrcData.Height;
            int SrcStride = SrcData.Stride, DestStride = DestData.Stride;
            byte* SrcP, DestP;
            for (int Y = 0; Y < Height; Y++)
            {
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride;         // 必须在某个地方开启unsafe功能，其实C#中的unsafe很safe，搞的好吓人。            
                DestP = (byte*)DestData.Scan0 + Y * DestStride;
                for (int X = 0; X < Width; X++, SrcP++, DestP++)
                {
                    *DestP = byte.MinValue;
                }
            }
            int region_size = coord_HistGramInRegions.Length;
            int[] mean = new int[region_size];
            for (int i = 0; i < region_size; i++)
            {
                int depths = 0;
                int coord_in_region_size = coord_HistGramInRegions[i].Count;
                if (coord_in_region_size == 0)
                {
                    //点的深度小于25或大于230，因此没有点在这个区域内容
                    mean[i] = 255;
                }
                else
                {
                    foreach (Coord coord in coord_HistGramInRegions[i])
                    {
                        int Y = coord.getY();
                        int X = coord.getX();
                        SrcP = (byte*)SrcData.Scan0 + Y * SrcStride + X;         // 必须在某个地方开启unsafe功能，其实C#中的unsafe很safe，搞的好吓人。            
                        depths = depths + *SrcP;
                    }
                    mean[i] = (int)(depths / coord_in_region_size);
                    Console.WriteLine("mean[i]" + i + " " + depths + " " + coord_in_region_size + " " + mean[i]);
                }
            }
            int minDepth = mean[0];
            for (int j = 0; j < region_size; j++)
            {
                if (minDepth >= mean[j])
                {
                    minDepth = mean[j];
                }
            }
            int minRegin = 0;
            for (int j = 0; j < region_size; j++)
            {
                if (minDepth == mean[j])
                {
                    minRegin = j;
                }
            }
            Console.WriteLine(minRegin);
            foreach (Coord coord in coord_HistGramInRegions[minRegin])
            {
                int Y = coord.getY();
                int X = coord.getX();
                SrcP = (byte*)SrcData.Scan0 + Y * SrcStride + X;         // 必须在某个地方开启unsafe功能，其实C#中的unsafe很safe，搞的好吓人。            
                DestP = (byte*)DestData.Scan0 + Y * DestStride + X;
                *DestP = byte.MaxValue;     // 写成255和0，C#编译器不认。
            }
            Src.UnlockBits(SrcData);
            Dest.UnlockBits(DestData);
            DestPic.Invalidate();
        }
        private void DoCoordHistBinaryzation(List<Coord>[] coord_HistGram, int Threshold)
        {
            if (Threshold == -1)
            {
                MessageBox.Show("选择了非法的阈值变量.");
                return;
            }
            for (int Y = 0; Y < 256; Y++)
            {
                   if(Y > Threshold)
                {
                    coord_HistGram[Y].Clear();
                }
            }
        }
        private void coordsInRegion(List<Coord>[] coord_HistGramInRegions, List<Region> regions)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                coord_HistGramInRegions[i] = new List<Coord>();
            }
            for(int j = 0; j<256; j++ )
            {
                foreach (Coord coord in coord_HistGram[j])
                {
                    Point p = new Point(coord.getX(), coord.getY());
                    //返回判断点是否在多边形里
                    int k = 0;
                    foreach (Region region in regions)
                    {
                        bool isInRegion = region.IsVisible(p);
                        if (isInRegion)
                        {
                            coord_HistGramInRegions[k].Add(coord);
                        }
                        k++;
                    }
                }
            }
        }
        private void makeRegionsFromHistGramEdges(List<Coord>[] coord_HistGramEdges, List<Region> regions)
        {
            for (int i = 0; i < coord_HistGramEdges.Length; i++)
            {
                List<Coord> coords = coord_HistGramEdges[i];
                System.Drawing.Drawing2D.GraphicsPath myGraphicsPath = new System.Drawing.Drawing2D.GraphicsPath();
                Region myRegion = new Region();
                myGraphicsPath.Reset();

                //添家多边形点
                Point[] Points = new Point[coords.Count];
                int j = 0;
                foreach (Coord coord in coords)
                {
                    Point p = new Point(coord.getX(), coord.getY());
                    Points[j] = p;
                    j++;
                }
                myGraphicsPath.AddPolygon(Points);
                myRegion.MakeEmpty();
                myRegion.Union(myGraphicsPath);
                //byte[] data = myRegion.GetRegionData().Data;
                //Console.WriteLine("myRegion.data.length " + data.Length);
                regions.Add(myRegion);
            }
        }
        private void AnalyzeCoordHist(List<Coord>[] coord_HistGram, string fileName)
        {
            String str = "";
            List<Coord>[] coord_HistGramEdges = null;
            try
            {
                FileStream file = new FileStream(fileName, FileMode.Open);
                StreamReader sr = new StreamReader(file);
                str = sr.ReadLine();
                int size = Convert.ToInt32(str);
                coord_HistGramEdges = new List<Coord>[size];
                for (int Y = 0; Y < size; Y++)
                {
                    coord_HistGramEdges[Y] = new List<Coord>();
                }
                int num_Edges = -1;
                str = sr.ReadLine();
                do
                {
                    //Console.WriteLine("str" + str);
                    if (str.Equals("-"))
                    {
                        num_Edges++;
                        coord_HistGramEdges[num_Edges] = new List<Coord>();
                    }
                    else
                    {
                        char split = ' ';
                        string[] strs = str.Split(split);
                        int y = Convert.ToInt32(strs[0]);
                        int x = Convert.ToInt32(strs[1]);
                        //Console.WriteLine("y" + y + "x" + x);
                        Coord coord = new Coord();
                        coord.setX(x);
                        coord.setY(y);
                        coord_HistGramEdges[num_Edges].Add(coord);
                    }
                    str = sr.ReadLine();
                } while (str != null);
                sr.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            List<Region> regions = new List<Region>();
            makeRegionsFromHistGramEdges(coord_HistGramEdges, regions);
            int region_size = regions.Count;
            List<Coord>[] coord_HistGramInRegions = new List<Coord>[region_size];
            coordsInRegion(coord_HistGramInRegions, regions);
            Console.WriteLine("regions.Count" + regions.Count);
            DoParts(SrcBmp, DestBmp, coord_HistGramInRegions);
            //return str;
        }

        private int GetThreshold()
        {
            switch (CmbMethod.SelectedItem.ToString())
            {
                case "灰度平均值":
                    return Threshold.GetMeanThreshold(HistGram);
                case "黄式模糊阈值":
                    return Threshold.GetHuangFuzzyThreshold(HistGram);
                case "谷底最小值":
                    return Threshold.GetMinimumThreshold(HistGram, HistGramS, ref n_mutlpyY, mutlpyY);
                    //return Threshold.GetMinimumThresholdWithCoord(coord_HistGram, coord_HistGramSmooth);
                case "位置相关谷底最小值":
                    return Threshold.GetMinimumThreshold(HistGram, HistGramS, ref n_mutlpyY, mutlpyY);
                case "双峰平均值":
                    return Threshold.GetIntermodesThreshold(HistGram,  HistGramS);
                case "百分比阈值":
                    return Threshold.GetPTileThreshold(HistGram);
                case "迭代阈值法":
                    return Threshold.GetIterativeBestThreshold(HistGram);
                case "大津法":
                    return Threshold.GetOSTUThreshold(HistGram);
                case "一维最大熵":
                    return Threshold.Get1DMaxEntropyThreshold(HistGram);
                case "动能保持":
                    return Threshold.GetMomentPreservingThreshold(HistGram);
                case "Kittler最小错误":
                    return Threshold.GetKittlerMinError(HistGram);
                case "ISODATA法":
                    return Threshold.GetIsoDataThreshold(HistGram);
                case "Shanbhag法":
                    return Threshold.GetShanbhagThreshold(HistGram);
                case "Yen法":
                    return Threshold.GetYenThreshold(HistGram);
                default:
                    break;
            }
            return -1;
        }

        public void DrawHistGram(Bitmap SrcBmp,int []Histgram)
        {
            BitmapData HistData = SrcBmp.LockBits(new Rectangle(0, 0, SrcBmp.Width, SrcBmp.Height), ImageLockMode.ReadWrite, SrcBmp.PixelFormat);
            int X, Y, Max = 0;
            byte* P;
            for (Y = 0; Y < 256; Y++) if (Max < Histgram[Y]) Max = Histgram[Y];
            for (X = 0; X < 256; X++)
            {
                P = (byte*)HistData.Scan0 + X;
                for (Y = 0; Y < 100; Y++)
                {
                    if ((100 - Y) > Histgram[X] * 100 / Max)
                        *P = 220;
                    else
                        *P = 0;
                    P += HistData.Stride;
                }
            }

           P = (byte*)HistData.Scan0 + Thr;
            //P = (byte*)HistData.Scan0;
            for (Y = 0; Y < 100; Y++)
            {
                *P = 255;
                P += HistData.Stride;
            }
            SrcBmp.UnlockBits(HistData);
        }

        private void CmbMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Init == true) Update();
        }
        private void Update()
        {
            Thr = GetThreshold();
            /*
            Console.WriteLine("Thr" + Thr);
            if (Thr == -1)
            {
                Console.WriteLine("n_mutlpyY=" + n_mutlpyY + "mutlpyY[0]=" + mutlpyY[0]);
                Thr = mutlpyY[0];
            }
             */
            if (Thr != -1)
            {
                //正常150个Iter以内
                DoBinaryzationLessThr(SrcBmp, DestBmp, Thr);
                DrawHistGram(HistBmp, HistGram);
                PicHist.Invalidate();
                if (CmbMethod.SelectedItem.ToString() == "谷底最小值" || CmbMethod.SelectedItem.ToString() == "双峰平均值")
                {
                    DrawHistGram(SmoothHistBmp, HistGramS);
                    PicSmoothHist.Invalidate();
                }
            }
            else
            {
                if(n_mutlpyY != 0)
                {
                    for (int j = 0; j < n_mutlpyY; j++)
                    {
                        Console.WriteLine("mutlpyY=" + mutlpyY[j]);
                    }
                    //有多个谷底的情况
                    Thr = mutlpyY[0];
                    DoBinaryzation(SrcBmp, DestBmp, Thr);
                    DrawHistGram(HistBmp, HistGram);
                    PicHist.Invalidate();
                    if (CmbMethod.SelectedItem.ToString() == "谷底最小值" || CmbMethod.SelectedItem.ToString() == "双峰平均值")
                    {
                        DrawHistGram(SmoothHistBmp, HistGramS);
                        PicSmoothHist.Invalidate();
                    }
                }
                else
                {

                }
            }
            n_mutlpyY = 0;
        }
        private void Update(int fileNum)
        {
            Thr = GetThreshold();
            if (Thr != -1)
            {
                //正常150个Iter以内
                DoBinaryzationLessThr(SrcBmp, DestBmp, Thr);
                String binImageName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\test\\CNN_data_test\\pieces_{0:0000000}_0.png", fileNum);
                DestPic.Image.Save(binImageName);
                DoBinaryzationLargeThr(SrcBmp, DestBmp, Thr);
                binImageName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\test\\CNN_data_test\\pieces_{0:0000000}_1.png", fileNum);
                DestPic.Image.Save(binImageName);
                DrawHistGram(HistBmp, HistGram);
                PicHist.Invalidate();
                if (CmbMethod.SelectedItem.ToString() == "谷底最小值" || CmbMethod.SelectedItem.ToString() == "双峰平均值")
                {
                    DrawHistGram(SmoothHistBmp, HistGramS);
                    PicSmoothHist.Invalidate();
                }
            }
            else
            {
                if (n_mutlpyY != 0)
                {
                    //有多个谷底的情况
                    String binImageName;
                    for (int j = 0; j <= n_mutlpyY; j++)
                    {
                        if (j == 0)
                        {
                            //第一段
                            Thr = mutlpyY[j];
                            DoBinaryzationLessThr(SrcBmp, DestBmp, Thr);
                        }
                        else if (j <= (n_mutlpyY - 1))
                        {
                            //Console.WriteLine("mutlpyY=" + mutlpyY[j]);
                            int left = mutlpyY[j - 1];
                            int right = mutlpyY[j];
                            DoBinaryzationBetweenTwoThrs(SrcBmp, DestBmp, left, right);
                        }
                        else if (j >= n_mutlpyY)
                        {
                            //最后一段
                            Thr = mutlpyY[(n_mutlpyY - 1)];
                            DoBinaryzationLargeThr(SrcBmp, DestBmp, Thr);
                        }
                        binImageName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\test\\CNN_data_test\\pieces_{0:0000000}_" + j + ".png", fileNum);
                        Console.WriteLine("binImageName=" + binImageName);
                        DestPic.Image.Save(binImageName);
                    }
                    DrawHistGram(HistBmp, HistGram);
                    PicHist.Invalidate();
                    if (CmbMethod.SelectedItem.ToString() == "谷底最小值" || CmbMethod.SelectedItem.ToString() == "双峰平均值")
                    {
                        DrawHistGram(SmoothHistBmp, HistGramS);
                        PicSmoothHist.Invalidate();
                    }
                }
                else
                {

                }
            }
            n_mutlpyY = 0;
        }
        /*
        private void Update(int fileNumWithRegions)
        {
            Thr = GetThreshold();
            //Console.WriteLine("Thr" + Thr);
            if (Thr != -1 && Thr != 1)
            {
                DoBinaryzation(SrcBmp, DestBmp, Thr);
                DoCoordHistBinaryzation(coord_HistGram, Thr);
                String FileName = String.Format("H:\\BMVC_DeROT\\dy_y+5000_binImages\\regionsInfoWithError\\regions_{0:0000000}-" + fileNumWithRegions + ".txt", fileNumWithRegions);
                AnalyzeCoordHist(coord_HistGram, FileName);
            }
            DrawHistGram(HistBmp, HistGram);
            PicHist.Invalidate();
            if (CmbMethod.SelectedItem.ToString() == "谷底最小值" || CmbMethod.SelectedItem.ToString() == "双峰平均值")
            {
                DrawHistGram(SmoothHistBmp, HistGramS);
                PicSmoothHist.Invalidate();
            }
        }
        */
        private void CmdSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Bitmap files (*.Bitmap)|*.Bmp|Jpeg files (*.jpg)|*.jpg|Png files (*.png)|*.png";
            saveFileDialog.FilterIndex = 4;
            saveFileDialog.RestoreDirectory = true;
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                DestPic.Image.Save(saveFileDialog.FileName);

            }
        }

        private void batch_import(object sender, EventArgs e)
        {
            for (int fileNum = 20; fileNum <= 20; fileNum++)
            {
                //String FileName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\custom_data\\far_depth_images\\img_{0:0000000}.png", fileNum);
                String FileName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\test\\depthImages\\img_{0:0000000}.png", fileNum);
                Bitmap Temp = (Bitmap)Bitmap.FromFile(FileName);
                if (IsGrayBitmap(Temp) == true)
                    SrcBmp = Temp;
                else
                {
                    SrcBmp = ConvertToGrayBitmap(Temp);
                    Temp.Dispose();
                }
                DestBmp = CreateGrayBitmap(SrcBmp.Width, SrcBmp.Height);
                DateTime beforDT = System.DateTime.Now;
                //耗时巨大的代码
                GetHistGram(SrcBmp, HistGram);
                DateTime afterDT = System.DateTime.Now;
                TimeSpan ts = afterDT.Subtract(beforDT);
                Console.WriteLine("DateTime总共花费{0}ms.", ts.TotalMilliseconds);
                /*
                if(GetThreshold()==-1)
                {
                    Console.WriteLine(fileNum);
                    using (StreamWriter sw = new StreamWriter("TestFile.txt", true))
                    {
                        sw.Write("The date is: ");
                        sw.WriteLine(fileNum);
                    }
                }
                */
                SrcPic.Image = SrcBmp;
                DestPic.Image = DestBmp;
                Update(fileNum);
                //String binImageName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\custom_data\\far_bin_images\\bin_{0:0000000}.png", fileNum);
                //String binImageName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\binImages\\bin_{0:0000000}.png", fileNum);
                //DestPic.Image.Save(binImageName);
                //Console.WriteLine("1");
            }
        }

        private void computeHandPixNum_Click(object sender, EventArgs e)
        {
            for(int i=0; i<256; i++)
            {
                ArrayList vecHistNumPerPostion = new ArrayList();
                vecHistNumS.Add(vecHistNumPerPostion);
            }
            for (int fileNum = 1; fileNum <= 202197; fileNum++)
            {
                String FileName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\testHands\\img_{0:0000000}.png", fileNum);
                //printf('H:\\BMVC_DeROT\\HistgramBinaryzation\\depthImages\\img__%07d.mat',fileNum);
                //String FileName = "H:\\BMVC_DeROT\\HistgramBinaryzation\\depthImages\\img_0000001.png";
                Bitmap Temp = (Bitmap)Bitmap.FromFile(FileName);
                if (IsGrayBitmap(Temp) == true)
                    SrcBmp = Temp;
                else
                {
                    SrcBmp = ConvertToGrayBitmap(Temp);
                    Temp.Dispose();
                }
                DestBmp = CreateGrayBitmap(SrcBmp.Width, SrcBmp.Height);
                GetHistGram(SrcBmp, HistGram);

                int N, accumulatedHand = 0;
                bool isGetPosition = false;
                ArrayList vecHistNumPerPostion = null;
                for (N = 26; N < 230; N++)
                {
                    accumulatedHand = accumulatedHand + HistGram[N];
                    if (accumulatedHand > 500 && isGetPosition ==false)
                    {
                        isGetPosition = true;
                        //Console.WriteLine("fileNum is" + fileNum + "HistGram[N]>500 and N is" + N);
                        vecHistNumPerPostion = (ArrayList)vecHistNumS[N];
                    }
                }
                if (vecHistNumPerPostion != null)
                {
                    vecHistNumPerPostion.Add(accumulatedHand);
                    //Console.WriteLine("accumulatedHand" + accumulatedHand);
                }
            }
            for (int i = 0; i < 256; i++)
            {
                ArrayList vecHistNumPerPostion = (ArrayList)vecHistNumS[i];
                if(vecHistNumPerPostion.Count!=0)
                {
                    Console.Write( i + ", " );
                }
            }
            Console.WriteLine(" ");
            for (int i = 0; i < 256; i++)
            {
                ArrayList vecHistNumPerPostion = (ArrayList)vecHistNumS[i];
                if (vecHistNumPerPostion.Count != 0)
                {
                    int sum = 0;
                    for (int j = 0; j < vecHistNumPerPostion.Count; j++)
                    {
                        //Console.WriteLine("vecHistNumPerPostion=" + i + "vecHistNum=" + vecHistNumPerPostion[j]);
                        sum = sum + (int)vecHistNumPerPostion[j];
                    }
                    avgNumPerPostion[i] = sum / vecHistNumPerPostion.Count;
                    Console.Write( avgNumPerPostion[i] + ", ");
                }
            }
        }

        private void CmdOpenForRegions_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Bitmap Temp = (Bitmap)Bitmap.FromFile(openFileDialog.FileName);
                if (IsGrayBitmap(Temp) == true)
                    SrcBmp = Temp;
                else
                {
                    SrcBmp = ConvertToGrayBitmap(Temp);
                    Temp.Dispose();
                }
                DestBmp = CreateGrayBitmap(SrcBmp.Width, SrcBmp.Height);
                GetHistGram(SrcBmp, HistGram);
                GetHistGramWithCoordinate(SrcBmp, coord_HistGram);
                SrcPic.Image = SrcBmp;
                DestPic.Image = DestBmp;
                Update(25);

            }
            openFileDialog.Dispose();
        }

        private void batchImportForRegions_Click(object sender, EventArgs e)
        {
            String path = @"H:\\BMVC_DeROT\\dy_y+5000_binImages\\regionsInfoWithError";
            var files = Directory.GetFiles(path, "*.txt");
            foreach (var file in files)
            {
                //Console.WriteLine(file);
                string[] strs = file.Split('-');
                //Console.WriteLine(strs[1]);
                strs = strs[1].Split('.');
                //Console.WriteLine(strs[0]);
                int fileNum = Convert.ToInt32(strs[0]);
                //String FileName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\custom_data\\far_depth_images\\img_{0:0000000}.png", fileNum);
                String FileName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\depthImages\\img_{0:0000000}.png", fileNum);
                Bitmap Temp = (Bitmap)Bitmap.FromFile(FileName);
                if (IsGrayBitmap(Temp) == true)
                    SrcBmp = Temp;
                else
                {
                    SrcBmp = ConvertToGrayBitmap(Temp);
                    Temp.Dispose();
                }
                DestBmp = CreateGrayBitmap(SrcBmp.Width, SrcBmp.Height);
                GetHistGram(SrcBmp, HistGram);
                GetHistGramWithCoordinate(SrcBmp, coord_HistGram);
                if (GetThreshold() == -1)
                {
                    Console.WriteLine(fileNum);
                    using (StreamWriter sw = new StreamWriter("TestFile.txt", true))
                    {
                        sw.Write("The date is: ");
                        sw.WriteLine(fileNum);
                    }
                }
                SrcPic.Image = SrcBmp;
                DestPic.Image = DestBmp;
                Update(fileNum);
                //String binImageName = String.Format("H:\\BMVC_DeROT\\HistgramBinaryzation\\custom_data\\far_bin_images\\bin_{0:0000000}.png", fileNum);
                String binImageName = String.Format("H:\\BMVC_DeROT\\dy_y+5000_binImages\\binImagesWithRegion\\bin_{0:0000000}.png", fileNum);
                DestPic.Image.Save(binImageName);
                //Console.WriteLine("1");
            }
        }

    }
}
