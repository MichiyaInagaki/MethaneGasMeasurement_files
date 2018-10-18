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
        static void Main(string[] args)
        {
            //初期設定///////////////////////////////////////////////////////////////////////////////////////////////////
            int i;
            TimeSpan offset = new TimeSpan(1, 0, 12, 0, 0);    //TimeSpan(日，時間，分，秒，ミリ秒)    //PTUとAndroidの時間ずれ***
            TimeSpan interval = new TimeSpan(0, 0, 0, 0, 500);  //時間の正規化間隔***

            //PTUデータの読み込み********************************************************************************
            ///////////////////////////////////////////////////////////////////////////////////////
            ////    PTUtimestamp : DateTime型 : PTUのタイムスタンプ [yyyy/MM/dd/HH:mm:ss.fff]  ////
            ////    pos_pan      : int型      : Panポジション [position]                       //// 
            ////    pos_tilt     : int型      : Tiltポジション [position]                      ////
            ///////////////////////////////////////////////////////////////////////////////////////

            List<List<string>> PTU_data = null;     //PTUデータ取得用多次元リスト

            //PTUデータの読み込み//////////@読み込みファイルパス，ファイル名
            using (var csv = new CsvReader(@"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\PTUSample.csv"))     
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
            PTUtimestamp = PTUtimestamp.ConvertAll(x => RoundUp(x, interval));

            //リストの長さの取得
            int length_PTUdata = PTUtimestamp.Count;

            ////PTUリストの表示確認
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

            //LMm-Gデータの読み込み/////////@読み込みファイルパス，ファイル名
            using (var csv2 = new CsvReader(@"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\LMmSample3.csv"))     
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
            LMmtimestamp = LMmtimestamp.ConvertAll(x => RoundUp(x, interval));

            //リストの表示確認
            for (i = 0; i < Length_LMmList; i++)
            {
                Console.WriteLine("LMm_TIME:" + LMmtimestamp[i].ToString("yyyy/MM/dd/HH:mm:ss.fff") + " LMm_measure:"+LMmmeasure[i]);
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
                for ( ; LMm_data_num < Length_LMmList; LMm_data_num++)   //LMmデータを走査していく
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
            int Length_List = Synchro_time.Count;            //行数を得る
            for (i = 0; i < Length_List; i++)
            {
                Console.WriteLine("TIME:" + Synchro_time[i].ToString("yyyy/MM/dd/HH:mm:ss.fff") + " Pan:" + PTU_Pan[i]+" Tilt:"+PTU_Tilt[i]+" LMm_Measure:"+ LMm_Measure[i]);
            }
            Console.WriteLine();
            //END時間同期させたデータセットの作成*********************************************************************

        }

        //時間の正規化（切り上げ）のための関数
        public static DateTime RoundUp(DateTime input, TimeSpan interval)
            => new DateTime(((input.Ticks + interval.Ticks - 1) / interval.Ticks) * interval.Ticks, input.Kind);

        //分割光路長の計算
        //private static double CalOP(int pan, int tilt)
        //{
        //    ///////ロボットプラットフォームの寸法設定[m]////////////////////////////
        //    double Height = 1.0;                             //PTUの高さ[m]
        //    double length_tilt = 0.038 + 0.01 + 0.02;        //Tilt部分の腕の長さ+取り付け具の厚み+メタン計の中心まで[m]
        //    double length_pan = 0.019;                       //レーザーの発射口とファイ回転軸中心からのずれ[m]
        //    double length_LMmG = 0.09;                       //回転中心からのLMｍ－Gの長さ[m](=LMm-Gの長さの半分)
        //    ///////分解能の設定，角度変換用の数値///////////////////////////////////
        //    double resolition = 185.1429;       //分解能
        //    double degpos = resolition / 3600;  //[degree/position]
        //    double deg_pan, deg_tilt;
        //    ///
        //    double op, Hl, Rl, R;
        //    //角度変換[pos]→[deg]
        //    deg_pan = pan * degpos;
        //    deg_tilt = tilt * degpos;
        //    //
            

        //    return degpos;

        //}
    }
}
