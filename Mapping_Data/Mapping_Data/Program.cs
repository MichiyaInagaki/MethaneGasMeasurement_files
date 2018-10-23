using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Mapping_Data
{
    class Program
    {
        //各種初期設定*************************************************************************************************
        //////CSVデータ読み込み//@"読み込みファイルパス，ファイル名.csv"///////////////////////////
        private static string PTU_data_path = @"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\PTUSample.csv";
        private static string LMm_data_path = @"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\LMmSample3.csv";
        //////同期時間の設定///////////////////////////////////////////////////////////////////////
        private static TimeSpan offset = new TimeSpan(1, 0, 12, 0, 0);     //TimeSpan(日，時間，分，秒，ミリ秒)    //PTUとAndroidの時間ずれ***
        private static TimeSpan interval_PTU = new TimeSpan(0, 0, 0, 0, 500);  //PTUの時間の正規化間隔***
        private static TimeSpan interval_LMm = new TimeSpan(0, 0, 0, 0, 500);  //LMmの時間の正規化間隔***
        ///////ロボットプラットフォームの寸法設定[m]///////////////////////////////////////////////
        private static double Height = 0.5;                             //PTUの高さ[m]
        private static double length_tilt = 0.038 + 0.01 + 0.02;        //Tilt部分の腕の長さ+取り付け具の厚み+メタン計の中心まで[m]
        private static double length_pan = 0.019;                       //レーザーの発射口とファイ回転軸中心からのずれ[m]
        private static double length_LMmG = 0.09;                       //回転中心からのLMｍ－Gの長さ[m](=LMm-Gの長さの半分)
        private static double resolition = 185.1429;                    //分解能
        ///////計測範囲，計測の刻み幅（!!計測時と同じもの!!）***///////////////////////////////////
        private static double delta = 0.5;  //刻み幅
        private static double xmin = -1.0;
        private static double xmax = 1.0;
        private static double ymin = 2.0;
        private static double ymax = 4.0;
        private static double zmax = Height + length_tilt;
        //ボクセル設定用////////////////////////////////////////////////////////////////////////////
        private static int Xrange = (int)(xmax / delta) - (int)(xmin / delta);            //x方向のセルの数
        private static int Yrange = (int)(ymax / delta);                                          //y方向のセルの数　※yminは考慮しない（LMm-Gの始点はy=0にあるため）
        private static int Zrange = (int)(RoundUp(Height + length_tilt, delta) / delta);  //z方向のセルの数（PTUの高さを分解能で切り上げたもの）
        private static int cell_size = Xrange * Yrange * Zrange;                          //すべてのセルの数
        ///////計測点の総数////////////////////////////////////////////////////////////////////////
        private static int measure_num = (Xrange + 1) * ((int)(ymax / delta) - (int)(ymin / delta) + 1);
        //光路の分割数の最大値（仮）///////////////////////////////////////////////////////////////
        private static int temp_len = Xrange / 2 + Yrange + Zrange;
        //交点を表現するための構造体の定義/////////////////////////////////////////////////////////
        public struct INTERSECTION
        {
            public double x;   //x座標
            public double y;   //y座標
            public double z;   //z座標
            public double len; //原点からの距離
            public double op;  //分割光路長
            public int num;    //ボクセルナンバー
        }
        //分割光路を格納する配列[計測点の数,ボクセルの数]=OP //これが欲しいデータリスト!////////////
        private static double[,] split_OP = new double[measure_num, cell_size+1];
        //END各種初期設定**********************************************************************************************

        static void Main(string[] args)
        {
            int i, j;

            //初期化関係*****************************************************************************************
            //分割光路長を格納する配列の初期化
            for (i = 0; i < measure_num; i++)
            {
                for (j = 0; j <= cell_size; j++)
                {
                    split_OP[i, j] = 0;
                }
            }
            //END初期化関係**************************************************************************************

            //PTUデータの読み込み********************************************************************************
            ///////////////////////////////////////////////////////////////////////////////////////
            ////    PTUtimestamp : DateTime型 : PTUのタイムスタンプ [yyyy/MM/dd/HH:mm:ss.fff]  ////
            ////    pos_pan      : int型      : Panポジション [position]                       //// 
            ////    pos_tilt     : int型      : Tiltポジション [position]                      ////
            ///////////////////////////////////////////////////////////////////////////////////////

            List<List<string>> PTU_data = null;     //PTUデータ取得用多次元リスト

            //PTUデータの読み込み
            using (var csv = new CsvReader(PTU_data_path))
            {
                PTU_data = csv.ReadToEnd();
            }

            //PTUデータをタイムスタンプ，パン角，チルト角の要素に分けて取得
            List<string> str_PTUtimestamp = PTU_data[0];
            List<string> str_pos_pan = PTU_data[1];
            List<string> str_pos_tilt = PTU_data[2];

            //string型からのキャスト
            List<DateTime> PTUtimestamp = str_PTUtimestamp.ConvertAll(x => System.DateTime.ParseExact(x,
            "yyyy/MM/dd/HH:mm:ss.fff",
            System.Globalization.DateTimeFormatInfo.InvariantInfo,
            System.Globalization.DateTimeStyles.None));                              //PTUのタイムスタンプ
            List<int> pos_pan = str_pos_pan.ConvertAll(x => int.Parse(x));           //Pan角
            List<int> pos_tilt = str_pos_tilt.ConvertAll(x => int.Parse(x));         //Tilt角

            //***PTUとAndroidの時間ずれ(オフセット)の修正***
            PTUtimestamp = PTUtimestamp.ConvertAll(x => x - offset);

            //時間の正規化（数値の丸め, 切り上げ）
            PTUtimestamp = PTUtimestamp.ConvertAll(x => Time_RoundUp(x, interval_PTU));

            //リストの長さの取得
            int length_PTUdata = PTUtimestamp.Count;

            //PTUリストの表示確認
            Console.WriteLine("■PTUデータの表示");
            for (i = 0; i < length_PTUdata; i++)
            {
                Console.WriteLine("PTU_TIME:" + PTUtimestamp[i].ToString("yyyy/MM/dd/HH:mm:ss.fff") + " PAN:" + pos_pan[i] + " TILT:" + pos_tilt[i]);
            }
            Console.WriteLine();
            //END:PTUデータ読み込み*******************************************************************************

            //LMm-Gデータ読み込み*********************************************************************************
            ///////////////////////////////////////////////////////////////////////////////////////
            ////    LMmtimestamp : DateTime型 : LMmのタイムスタンプ [yyyy/MM/dd/HH:mm:ss.fff]  ////
            ////    LMmmeasure   : int型      : LMmの計測値 [ppm-m]                            //// 
            ///////////////////////////////////////////////////////////////////////////////////////

            List<List<string>> LMm_data = null;

            //LMm-Gデータの読み込み
            using (var csv2 = new CsvReader(LMm_data_path))
            {
                LMm_data = csv2.ReadToEnd();
            }

            //LMm-Gデータをタイムスタンプ，測定値に分けて取得
            int Length_LMmList = LMm_data.Count;            //行数を得る
            var str_LMmtimestamp = new List<string>();      //LMmのタイムスタンプ格納用リスト
            var str_LMmmeasure = new List<string>();        //LMmの測定値格納用リスト

            //※データが縦列に並んでいるので，改めて列で読み込んでいく(CsvReader.csは行で読み込んでいるため，この処理が必要)
            for (i = 0; i < Length_LMmList; i++)
            {
                str_LMmtimestamp.Add(LMm_data[i][1]);        //LMｍのタイムスタンプリストの作成
            }

            for (i = 0; i < Length_LMmList; i++)
            {
                str_LMmmeasure.Add(LMm_data[i][3]);        //LMｍの計測リストの作成
            }

            //string型からのキャスト
            List<DateTime> LMmtimestamp = str_LMmtimestamp.ConvertAll(x => System.DateTime.ParseExact(x,
            "yyyy/MM/dd HH:mm:ss.f",
            System.Globalization.DateTimeFormatInfo.InvariantInfo,
            System.Globalization.DateTimeStyles.None));                             //LMmのタイムスタンプ
            List<int> LMmmeasure = str_LMmmeasure.ConvertAll(x => int.Parse(x));    //LMm計測データ

            //時間の正規化
            LMmtimestamp = LMmtimestamp.ConvertAll(x => Time_RoundUp(x, interval_LMm));

            //リストの表示確認
            Console.WriteLine("■LMm-Gデータの表示");
            for (i = 0; i < Length_LMmList; i++)
            {
                Console.WriteLine("LMm_TIME:" + LMmtimestamp[i].ToString("yyyy/MM/dd/HH:mm:ss.fff") + " LMm_measure:" + LMmmeasure[i]);
            }
            Console.WriteLine();
            //END:LMm-Gデータ読み込み******************************************************************************

            //時間同期させたデータセットの作成*********************************************************************
            //////////////////////////////////////////////////////////////////////////////////////////////////
            ////    Synchro_time : List<DateTime>型 : 同期時刻のタイムスタンプ [yyyy/MM/dd/HH:mm:ss.fff]  ////
            ////    PTU_Pan      : List<int>型      : Pan  [position]                                     //// 
            ////    PTU_Tilt     : List<int>型      : Tilt [position]                                     ////
            ////    LMm_Measure  : List<int>型      : LMm-Gの測定値 [ppm-m]                               ////
            //////////////////////////////////////////////////////////////////////////////////////////////////

            int PTU_data_num = 0, LMm_data_num = 0;
            List<int> PTU_Pan = new List<int>();            //PTUのPan角を格納
            List<int> PTU_Tilt = new List<int>();           //PTUのTilt角を格納
            List<int> LMm_Measure = new List<int>();        //LMm-Gの計測データを格納
            List<DateTime> Synchro_time = new List<DateTime>(); //同期時刻データを格納
            i = 0;
            TimeSpan span = new TimeSpan();                     //タイムスタンプの差を格納
            TimeSpan null_time = new TimeSpan(0, 0, 0, 0, 0);   //NULL時間

            for (PTU_data_num = 0; PTU_data_num < length_PTUdata; PTU_data_num++)       //PTUデータを走査していく
            {
                for (; LMm_data_num < Length_LMmList; LMm_data_num++)                  //LMmデータを走査していく
                {
                    span = PTUtimestamp[PTU_data_num] - LMmtimestamp[LMm_data_num];     //タイムスタンプの時間差を取得

                    if (span == null_time)      //PTUとLMｍの時間が同じとき，データを格納する
                    {
                        PTU_Pan.Add(pos_pan[PTU_data_num]);
                        PTU_Tilt.Add(pos_tilt[PTU_data_num]);
                        LMm_Measure.Add(LMmmeasure[LMm_data_num]);
                        Synchro_time.Add(PTUtimestamp[PTU_data_num]);
                        break;                  //現在のPTUデータと一致するのは1つなので，一致すればこのループを抜ける
                    }

                    if ((int)span.TotalMilliseconds < 0)
                    {
                        break;                  //現在のPTUデータと一致するものがなければ，次のPTUデータへ
                    }
                }
            }

            //リストの表示確認
            int Length_List = Synchro_time.Count;            //行数を得る=計測数
            Console.WriteLine("■同期済みデータの表示");
            for (i = 0; i < Length_List; i++)
            {
                Console.WriteLine("TIME:" + Synchro_time[i].ToString("yyyy/MM/dd/HH:mm:ss.fff") + " Pan:" + PTU_Pan[i] + " Tilt:" + PTU_Tilt[i] + " LMm_Measure:" + LMm_Measure[i]);
            }
            Console.WriteLine();
            //END時間同期させたデータセットの作成*********************************************************************


            //分割光路の計算******************************************************************************************
            //ボクセルナンバーをふる
            create_BOXCELL_num();

            //hoge
            //double hoge = get_BOXCELL_num(-1,0.9,0.039);
            //Console.WriteLine("hoge:"+hoge);
            //CalOP(526,-499,1);

            //計算部
            for (i = 0; i < Length_List; i++)
            {
                CalOP(PTU_Pan[i], PTU_Tilt[i], i);
            }

            //表示確認
            for(i=0;i< measure_num; i++)
            {
                for (j = 1; j < cell_size + 1; j++)
                {
                    Console.WriteLine("measure_num:"+i+" cell_num:"+j+" OP:"+split_OP[i, j]);
                }
            }
            //END分割光路の計算***************************************************************************************
        }


        //各種関数，静的変数など**************************************************************************************
        //時間の正規化（切り上げ）のための関数///////////////////////////////////////////////////////
        public static DateTime Time_RoundUp(DateTime input, TimeSpan interval)
            => new DateTime(((input.Ticks + interval.Ticks - 1) / interval.Ticks) * interval.Ticks, input.Kind);
        //数値の丸めを行う関数///////////////////////////////////////////////////////////////////////
        //切り上げ(データ，切り上げ間隔)
        public static double RoundUp(double data, double interval)
        {
            if (data >= 0)
            {
                return (int)((data + interval - 0.001) / interval) * interval;    //-0.001は2.0ジャストなどを2とするためのもの
            }
            else
            {
                return (int)((data - interval + 0.001 ) / interval) * interval;
            }
        }
        //切り捨て（データ，切り捨て間隔）
        public static double RoundDown(double data, double interval)
        {
            return (int)(data / interval) * interval;
        }
        //END各種関数，静的変数など***********************************************************************************


        //分割光路長の計算********************************************************************************************
        private static void CalOP(int pan, int tilt, int MEASURE_NUMBER)
        {
            int i;
            ///////分解能の設定，角度変換用の数値///////////////////////////////////
            double degpos = resolition / 3600;  //[degree/position]
            double deg_pan, deg_tilt, rad_pan, rad_tilt;
            //角度変換[pos]→[deg] //PTU座標軸から平面グリッド座標軸に変換:[[-1かける]],[[90°から引く]]
            deg_tilt = 90 - (-tilt * degpos);
            if (-pan * degpos >= 0)      //phi>0,phi<0で場合分け
            {
                deg_pan = 90 - (-pan * degpos);
                //Console.WriteLine("degpan:" + deg_pan + " degtilt:" + deg_tilt);
            }
            else
            {
                deg_pan = 90 - (pan * degpos);
                //Console.WriteLine("degpan:" + (180-deg_pan) + " degtilt:" + deg_tilt);
            }
            //角度変換[deg]→[rad] //＋ファイドット，シータドットに変換
            rad_pan = deg_pan * (Math.PI / 180);
            rad_tilt = deg_tilt * (Math.PI / 180);
            ///////ボクセル座標とボクセルナンバーを格納する構造体///////////////////
            INTERSECTION[] intersection_grid = new INTERSECTION[temp_len];
            //構造体の初期化
            for (i = 0; i < temp_len; i++)
            {
                intersection_grid[i].x = 0;
                intersection_grid[i].y = 0;
                intersection_grid[i].z = 0;
                intersection_grid[i].len = 1000;    //flag用に1000に設定
                intersection_grid[i].op = 1000;
                intersection_grid[i].num = -1;
            }
            //分割点の取得で使うもの
            int x_loop, y_loop, z_loop;     //ループ用変数
            double x_grid, y_grid, z_grid;  //一時的に交点の座標が入るもの
            int split_num = 0;              //分割数の初期化
            ////////////////////////////////////////////////////////////////////////


            //分割点の取得//////////////////////////////////////////////////////////
            if (-pan * degpos >= 0)      //x>=0のとき-------------------------------
            {
                x_loop = 1;     //xループの開始点
                if (xmin > 0)   //xminが正ならxループの開始点を変更する
                {
                    x_loop = (int)(xmin / delta);
                }

                //x走査
                for (; x_loop <= (int)(xmax / delta); x_loop++)  // x_loop = 1 は delta/delta のこと(ループ用に整数に直しているだけ)
                {
                    //Console.Write("x_loop ");
                    x_grid = x_loop * delta;    //グリッド座標に変換
                    y_grid = x_grid * Math.Tan(rad_pan);
                    z_grid = zmax - Math.Sqrt(x_grid * x_grid + y_grid * y_grid) / Math.Tan(rad_tilt);
                    //Console.WriteLine("x:" + x_grid + " y:" + y_grid + " z:" + z_grid);
                    if (y_grid > ymax || z_grid < 0)   //y_grid, z_gridが最終座標を超えればbreak
                    {
                        //Console.WriteLine("*break");
                        break;
                    }
                    //データ登録
                    intersection_grid[split_num].x = x_grid;
                    intersection_grid[split_num].y = y_grid;
                    intersection_grid[split_num].z = z_grid;
                    intersection_grid[split_num].len = Math.Sqrt(x_grid * x_grid + y_grid * y_grid + (zmax - z_grid) * (zmax - z_grid));
                    intersection_grid[split_num].num = get_BOXCELL_num(x_grid, y_grid, z_grid);
                    //Console.WriteLine("*num" + intersection_grid[split_num].num);
                    split_num++;
                }
                //y走査
                for (y_loop = 1; y_loop <= (int)(ymax / delta); y_loop++)
                {
                    //Console.Write("y_loop ");
                    y_grid = y_loop * delta;    //グリッドに変換
                    x_grid = y_grid / Math.Tan(rad_pan);
                    z_grid = zmax - Math.Sqrt(x_grid * x_grid + y_grid * y_grid) / Math.Tan(rad_tilt);
                    //Console.WriteLine("x:" + x_grid + " y:" + y_grid + " z:" + z_grid);
                    if (x_grid > xmax || z_grid < 0)
                    {
                        //Console.WriteLine("*break");
                        break;
                    }
                    //データ登録
                    intersection_grid[split_num].x = x_grid;
                    intersection_grid[split_num].y = y_grid;
                    intersection_grid[split_num].z = z_grid;
                    intersection_grid[split_num].len = Math.Sqrt(x_grid * x_grid + y_grid * y_grid + (zmax - z_grid) * (zmax - z_grid));
                    intersection_grid[split_num].num = get_BOXCELL_num(x_grid, y_grid, z_grid);
                    //Console.WriteLine("*num" + intersection_grid[split_num].num);
                    split_num++;
                }
                //z走査
                for (z_loop = Zrange - 1; z_loop >= 0; z_loop--)
                {
                    //Console.Write("z_loop ");
                    z_grid = z_loop * delta;     //グリッドに変換
                    y_grid = (zmax - z_grid) * Math.Tan(rad_tilt);
                    x_grid = y_grid / Math.Tan(rad_pan);
                    //Console.WriteLine("x:" + x_grid + " y:" + y_grid + " z:" + z_grid);

                    if (x_grid > xmax || y_grid > ymax)
                    {
                        //Console.WriteLine("*break");
                        break;
                    }
                    //データ登録
                    intersection_grid[split_num].x = x_grid;
                    intersection_grid[split_num].y = y_grid;
                    intersection_grid[split_num].z = z_grid;
                    intersection_grid[split_num].len = Math.Sqrt(x_grid * x_grid + y_grid * y_grid + (zmax - z_grid) * (zmax - z_grid));
                    intersection_grid[split_num].num = get_BOXCELL_num(x_grid, y_grid, z_grid);
                    //Console.WriteLine("*num" + intersection_grid[split_num].num);
                    split_num++;
                }
            }
            else    //x<0のとき---------------------------------------------------------------------------
            {
                x_loop = 1;     //xループの開始点
                if (xmax < 0)   //xmaxが負ならxループの開始点を変更する
                {
                    x_loop = -(int)(xmax / delta);
                }

                //x走査
                for (; x_loop <= -(int)(xmin / delta); x_loop++)  // x_loop = 1 は delta/delta のこと(ループ用に整数に直しているだけ)
                {
                    //Console.Write("x_loop ");
                    x_grid = x_loop * delta;    //グリッド座標に変換
                    y_grid = x_grid * Math.Tan(rad_pan);
                    z_grid = zmax - Math.Sqrt(x_grid * x_grid + y_grid * y_grid) / Math.Tan(rad_tilt);
                    //Console.WriteLine("x:" + x_grid + " y:" + y_grid + " z:" + z_grid);
                    if (y_grid > ymax || z_grid < 0)   //y_grid, z_gridが最終座標を超えればbreak
                    {
                        //Console.WriteLine("*break");
                        break;
                    }
                    //データ登録
                    intersection_grid[split_num].x = x_grid;
                    intersection_grid[split_num].y = y_grid;
                    intersection_grid[split_num].z = z_grid;
                    intersection_grid[split_num].len = Math.Sqrt(x_grid * x_grid + y_grid * y_grid + (zmax - z_grid) * (zmax - z_grid));
                    intersection_grid[split_num].num = get_BOXCELL_num(x_grid, y_grid, z_grid);
                    //Console.WriteLine("*num" + intersection_grid[split_num].num);
                    split_num++;
                }
                //y走査
                for (y_loop = 1; y_loop <= (int)(ymax / delta); y_loop++)
                {
                    //Console.Write("y_loop ");
                    y_grid = y_loop * delta;    //グリッドに変換
                    x_grid = - y_grid / Math.Tan(rad_pan);
                    z_grid = zmax - Math.Sqrt(x_grid * x_grid + y_grid * y_grid) / Math.Tan(rad_tilt);
                    //Console.WriteLine("x:" + x_grid + " y:" + y_grid + " z:" + z_grid);
                    if (x_grid < xmin || z_grid < 0)
                    {
                        //Console.WriteLine("*break");
                        break;
                    }
                    //データ登録
                    intersection_grid[split_num].x = x_grid;
                    intersection_grid[split_num].y = y_grid;
                    intersection_grid[split_num].z = z_grid;
                    intersection_grid[split_num].len = Math.Sqrt(x_grid * x_grid + y_grid * y_grid + (zmax - z_grid) * (zmax - z_grid));
                    intersection_grid[split_num].num = get_BOXCELL_num(x_grid, y_grid, z_grid);
                    //Console.WriteLine("*num" + intersection_grid[split_num].num);
                    split_num++;
                }
                //z走査
                for (z_loop = Zrange - 1; z_loop >= 0; z_loop--)
                {
                    //Console.Write("z_loop ");
                    z_grid = z_loop * delta;     //グリッドに変換
                    y_grid = (zmax - z_grid) * Math.Tan(rad_tilt);
                    x_grid = - y_grid / Math.Tan(rad_pan);
                    //Console.WriteLine("x:" + x_grid + " y:" + y_grid + " z:" + z_grid);

                    if (x_grid < xmin || y_grid > ymax)
                    {
                        //Console.WriteLine("*break");
                        break;
                    }
                    //データ登録
                    intersection_grid[split_num].x = x_grid;
                    intersection_grid[split_num].y = y_grid;
                    intersection_grid[split_num].z = z_grid;
                    intersection_grid[split_num].len = Math.Sqrt(x_grid * x_grid + y_grid * y_grid + (zmax - z_grid) * (zmax - z_grid));
                    intersection_grid[split_num].num = get_BOXCELL_num(x_grid, y_grid, z_grid);
                    //Console.WriteLine("*num" + intersection_grid[split_num].num);
                    split_num++;
                }
            }

            //ソート前のデータ確認
            //for (i = 0; i < temp_len; i++)
            //{
            //    Console.WriteLine("num:" + i + " x" + intersection_grid[i].x + " y:" + intersection_grid[i].y + " z:" + intersection_grid[i].z + " len:" + intersection_grid[i].len + " num:" + intersection_grid[i].num);
            //}
            //Console.WriteLine("SORT");

            //原点からの長さでソート//////////////////////////////////////////////////////////////
            SORT(intersection_grid, temp_len);
            //ソート確認
            //for (i = 0; i < temp_len; i++)
            //{
            //    Console.WriteLine("num:" + i + " x" + intersection_grid[i].x + " y:" + intersection_grid[i].y + " z:" + intersection_grid[i].z + " len:" + intersection_grid[i].len + " num:" + intersection_grid[i].num);
            //}
            //OPの計算//////////////////////////////////////////////////////////////////////////////
            for (i = 0; i < temp_len - 1; i++)
            {
                if (intersection_grid[i].num == -1) //データが入っていなければそこで計算終わり
                {
                    break;
                }
                if (i == 0)
                {
                    intersection_grid[i].op = intersection_grid[i].len - length_LMmG;   //LMｍから一番近い分割光路はLMｍの半分の長さ分だけ引く
                }
                else
                {
                    intersection_grid[i].op = intersection_grid[i].len - intersection_grid[i-1].len;    //OPの計算
                }
            }
            //opの計算確認
            //for (i = 0; i < temp_len; i++)
            //{
            //    Console.WriteLine("num:" + i + " x" + intersection_grid[i].x + " y:" + intersection_grid[i].y + " z:" + intersection_grid[i].z + " len:" + intersection_grid[i].len + " num:" + intersection_grid[i].num+" op:"+intersection_grid[i].op);
            //}
            //値を最終配列に入れる//////////////////////////////////////////////////////////////////
            for (i = 0; i < temp_len; i++)
            {
                if (intersection_grid[i].num == -1) //データが入っていなければそこで計算終わり
                {
                    break;
                }
                split_OP[MEASURE_NUMBER, intersection_grid[i].num] = intersection_grid[i].op;
            }
            //最終確認
            //Console.WriteLine("split_OP");
            //for (i = 1; i <= cell_size; i++)
            //{
            //    Console.WriteLine("num:"+i+" op:"+split_OP[MEASURE_NUMBER, i]);
            //}

        }
        //END分割光路長の計算*****************************************************************************************


        //ボクセルナンバー取得関係************************************************************************************
        //交点のグリッド座標からボクセルナンバーを取得する関数***
        private static int get_BOXCELL_num(double x, double y, double z)
        {
            int num;
            int boxcell_x = (int)(RoundUp(x, delta) / delta);   //RoundDown(x, delta):分解能まで正規化，/deltaでボクセル座標に変換
            int boxcell_y = (int)(RoundUp(y, delta) / delta);
            int boxcell_z = (int)(RoundDown(z, delta) / delta) + 1;
            num = trans_Boxcell_num(boxcell_x, boxcell_y, boxcell_z);

            return num;
        }

        //ボクセルナンバーを格納する配列
        private static int[,,] boxcell_num = new int[Xrange+2, Yrange+2, Zrange+2];
        //ボクセルナンバーをふる関数
        private static void create_BOXCELL_num()
        {
            int num = 1;
            int i, j, k;

            for (k = 0; k < Zrange; k++)
            {
                for (j = 0; j < Yrange; j++)
                {
                    for(i = 0; i < Xrange; i++)
                    {
                        boxcell_num[i, j, k] = num;
                        num++;
                    }
                }
            }
        }
        //ボクセル座標からボクセルナンバーを取得する関数//get_Boxcell_num[ボクセル座標（x,y,z）]
        private static int trans_Boxcell_num(int x, int y, int z)
        {
            if (x < 0)
            {
                x = x - (int)(xmin / delta);
                y = y - 1;
                z = z - 1;
            }
            else
            {
                x = x - (int)(xmin / delta) - 1;    //ボクセル座標のx座標には0がないので-1して詰める
                y = y - 1;
                z = z - 1;
            }
            return boxcell_num[x, y, z];
        }
        //ENDボクセルナンバー取得関係*********************************************************************************

        //構造体のソート関数
        private static void SORT(INTERSECTION[] inter, int max)
        {
            int i, j;
            INTERSECTION temp;
            for (i = 0; i < max-1; i++)
            {
                for (j = max-1; j > i; j--)
                {
                    if (inter[j - 1].len > inter[j].len)
                    {
                        temp = inter[j-1];
                        inter[j-1] = inter[j];
                        inter[j] = temp;
                    }
                }
            }
        }
    }
}
