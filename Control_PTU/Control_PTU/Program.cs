using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using System.IO;

namespace Control_PTU
{
    class Program
    {
        //CSVファイル出力先のパス格納用
        private static string output_path;

        static void Main(string[] args)
        {
            //接続部********************************************************************************************
            SerialPort port = new SerialPort("COM5", 9600, Parity.None, 8, StopBits.One);      //通信設定
            try
            {
                port.Open();
                port.DtrEnable = true;
                port.RtsEnable = true;
                port.DataReceived += new SerialDataReceivedEventHandler(serialPort1_DataReceived);
                Console.WriteLine("接続完了\n");
            }
            catch (Exception e)
            {
                Console.WriteLine("connection error! Unexpected exception : {0}", e.ToString());
            }
            //END接続部*****************************************************************************************

            //受信部********************************************************************************************
            void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
            {
                string str = port.ReadLine();   //受信信号読み込み
                string rec_str = "receive" +str;
                //Console.Write("受信:" + rec_str+"\n");  //受信信号表示***

                if (str.StartsWith("A"))        //A*(動作完了)が受信されたかどうかを見る
                {
                    AstarFlag = true; 
                }
                else
                {
                    AstarFlag = false;
                }

                if (str.StartsWith("MS"))        //移動完了フラグ（MSコマンドで1が返ってきたとき）
                {               
                    DateTime dt = DateTime.Now;                         //現在の時刻の取得
                    string DT = dt.ToString("yyyy/MM/dd/HH:mm:ss.fff"); //string型に変換（ミリ秒まで取得）
                    Console.WriteLine("\tTime stamp: "+ DT);            //タイムスタンプ***
                    //CSV書き込み////////////////////////////////////////////////////////////////////////
                    try
                    {
                        // appendをtrueにすると，既存のファイルに追記
                        //         falseにすると，ファイルを新規作成する
                        var append = true;
                        // 出力用のファイルを開く
                        using (var sw = new StreamWriter(output_path, append))
                        {
                            sw.WriteLine("{0}, {1}, {2},", DT, PAN, TILT);  //書き込み　時間，PAN, TILT
                        }
                    }
                    catch (Exception err)
                    {
                        // ファイルを開くのに失敗したときエラーメッセージを表示
                        Console.WriteLine(err.Message);
                    }
                    //////////////////////////////////////////////////////////////////////////////////////
                }
            }
            //END受信部*****************************************************************************************
            
            //コマンド選択部************************************************************************************
            while (true)
            {
                Console.WriteLine("sキー：実行 / qキー：中断 / dキー：接続終了");
                var key = Console.ReadKey(false);
                //接続終了
                if (key.KeyChar == 'd')
                {
                    Disconnect(port);
                    Environment.Exit(0);    //コンソールアプリケーションの終了
                }

                //実行部へ
                if (key.KeyChar == 's')
                {
                    ////////////////////////Exercute関数の仕様//////////////////////////////////////////
                    //Exercute(port, xmin, xmax, ymin, ymax, delta, output_path)                      //
                    //xmin, xmax, ymin, ymax: 測定の範囲（パンチルトの左右方向がx，前後方向がy）[m]   //
                    //delta: 測定の刻み幅[m]（!!測定範囲を割り切れるものにすること!!）                //
                    ////////////////////////////////////////////////////////////////////////////////////
                    //Exercute(port, -1.0, 1.0, 2.0, 4.0, 0.5, @"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\05MP_0.csv");
                    //Exercute(port, 0.0, 2.0, 2.0, 4.0, 0.5, @"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\05MP_2.csv");
                    //Exercute(port, -2.0, 0.0, 2.0, 4.0, 0.5, @"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\05MP_-2.csv");
                    Exercute(port, -1.0, 1.0, 2.0, 4.0, 0.2, @"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\MP_0.csv");
                    //Exercute(port, -2.0, 0.0, 2.0, 4.0, 0.2, @"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\MP_-2.csv");
                    //Exercute(port, 0.0, 2.0, 2.0, 4.0, 0.2, @"C:\Users\SENS\source\repos\Control_PTU\Control_PTU\csv\MP_2.csv");
                }

            }
            //ENDコマンド選択部*********************************************************************************
        }

        //中断ブレイク用フラグ
        private static bool breakFlag = false;

        //実行終了フラグ
        private static bool EndprocessFlag = false;
        
        //A*受信フラグ
        private static bool AstarFlag = false;

        //CSV書き込み用変数
        private static string PAN = "0";
        private static string TILT = "0";

        //実行部************************************************************************************************
        private static void Exercute(SerialPort port, double xmin, double xmax, double ymin, double ymax, double delta, string output)
        {
            //初期設定（計測範囲，最大角，変数設定）************************************************************
            int count = 1;                          //測定点の数カウント用
            ///////ループ用変数の設定///////////////////////////////////////////////
            int X_loop, Y_loop;                               //ループ用変数
            int XMIN, XMAX, YMIN, YMAX, DELTA;         //ループ用に[cm]に直す,int型にキャスト
            XMIN = (int)(xmin * 100);
            XMAX = (int)(xmax * 100);
            YMIN = (int)(ymin * 100);
            YMAX = (int)(ymax * 100);
            DELTA = (int)(delta * 100);
            ///////ロボットプラットフォームの寸法設定[m]////////////////////////////
            double Height = 1.0;                             //PTUの高さ[m]
            double length_tilt = 0.038 + 0.01 + 0.02;        //Tilt部分の腕の長さ+取り付け具の厚み+メタン計の中心まで[m]
            double length_pan = 0.019;                       //レーザーの発射口とファイ回転軸中心からのずれ[m]
            ///////最大角，最小角の設定[pos]////////////////////////////////////////
            int pos_pan_max, pos_pan_min, pos_tilt_max, pos_tilt_min;   
            pos_pan_max = 3094;
            pos_pan_min = -3095;
            pos_tilt_max = 593;
            pos_tilt_min = -908;
            ///////分解能の設定，角度変換用の数値///////////////////////////////////
            double resolition = 185.1429;       //分解能
            double degpos = resolition / 3600;  //[degree/position]
            double deg_pan, deg_tilt, pos_pan, pos_tilt;
            ///////角度計算用変数（ループの[cm]を[m]に直したものが入る）////////////
            double Xm, Ym;
            ///////フラグ初期化/////////////////////////////////////////////////////
            _keyReaded = false;                 
            breakFlag = false;
            EndprocessFlag = false;
            AstarFlag = false;
            //////ファイルパス読み込み//////////////////////////////////////////////
            output_path = output;
            //////中断用ボタンの読み込み////////////////////////////////////////////
            ThreadPool.QueueUserWorkItem(new WaitCallback(interruption), null);     
            //////Task処理関係//////////////////////////////////////////////////////////
            CancellationTokenSource tokenSource = new CancellationTokenSource();    //タスクのキャンセルをするためのもの
            ManualResetEvent mre = new ManualResetEvent(true);                      //スレッドを待機させるためのもの
            //////計測範囲のCSV書き込み////////////////////////////////////////////////////////////////////////
            try
            {
                // appendをtrueにすると，既存のファイルに追記
                //         falseにすると，ファイルを新規作成する
                var append = true;
                // 出力用のファイルを開く
                using (var sw = new StreamWriter(output_path, append))
                {
                    sw.WriteLine("{0}, {1}, {2}, {3}", xmin, xmax, ymin, ymax);  //書き込み　時間，PAN, TILT
                }
            }
            catch (Exception err)
            {
                // ファイルを開くのに失敗したときエラーメッセージを表示
                Console.WriteLine(err.Message);
            }
            //////////////////////////////////////////////////////////////////////////////////////
            //END初期設定***************************************************************************************

            var task = new Task(
                () => {
                    //実行部（メインの流れ）********************************************************************
                    //
                    //初期化関係
                    Console.Write("setup... \n");  //初期化
                    port.Write("S ");              //Slaved mode
                    port.Write("PS500 ");       //Pan速度設定
                    port.Write("TS500 ");       //Tilt速度設定
                    port.Write("PP00 ");        //Pan0
                    port.Write("TP00 ");        //Tilt0
                    port.Write("A ");
                    mre.Reset();            //一回停止させる
                    mre.WaitOne();
                    //
                    //時刻合わせ用タイムスタンプ
                    port.Write("I ");               //Immediare mode
                    port.Write("TP-200 ");        //***Tiltの動作角度入力***
                    DateTime dt = DateTime.Now;
                    string DT = dt.ToString("yyyy/MM/dd/HH:mm:ss.fff");      //string型に変換（ミリ秒まで取得）
                    Console.WriteLine("■同期開始時刻: " + DT + "\n");       //タイムスタンプ 
                    //CSV書き込み////////////////////////////////////////////////////////////////////////
                    try
                    {
                        // appendをtrueにすると，既存のファイルに追記
                        //         falseにすると，ファイルを新規作成する
                        var append = true;
                        // 出力用のファイルを開く
                        using (var sw = new StreamWriter(output_path, append))
                        {
                            sw.WriteLine("{0}, {1}, {2},", DT, 10000, 10000);  //書き込み　時間，PAN, TILT
                        }
                    }
                    catch (Exception err)
                    {
                        // ファイルを開くのに失敗したときエラーメッセージを表示
                        Console.WriteLine(err.Message);
                    }
                    //////////////////////////////////////////////////////////////////////////////////////
                    port.Write("S ");              //Slaved mode
                    Thread.Sleep(3000);            //***停止時間(3sec)***
                    //

                    for (Y_loop = YMIN; Y_loop <= YMAX; Y_loop = Y_loop + DELTA)      //y方向（縦方向）のループ
                    {
                        for (X_loop = XMIN; X_loop <= XMAX; X_loop = X_loop + DELTA)  //x方向（横方向）のループ[行きがけ：-から+]/////////////
                        {
                            Xm = X_loop / 100.0;  //計算用にxy座標を[m]に直す
                            Ym = Y_loop / 100.0;
                            deg_pan = CalPan(Xm, Ym, length_pan);                           //ファイ(degree)の取得
                            deg_tilt = CalTilt(Xm, Ym, Height, length_tilt, length_pan);    //シータ(degree)の取得
                            pos_pan = -deg_pan / degpos;        //Pan-potitionへの変換 :[[-かける]]
                            pos_tilt = -deg_tilt / degpos;      //Tilt-positionへの変換:[[-かける]]
                            pos_pan = Math.Round(pos_pan);      //数値の丸め（四捨五入）
                            pos_tilt = Math.Round(pos_tilt);
                            PAN = pos_pan.ToString();           //CSV書き込みPAN
                            TILT = pos_tilt.ToString();         //CSV書き込みPAN

                            //中断ボタン確認：qキーが押されれば中断する
                            if (_keyReaded)
                            {
                                Console.WriteLine("qキーが押されたので中断します");
                                breakFlag = true;
                            }

                            //動作範囲確認：動作範囲を越えた場合中断する
                            if (pos_pan > pos_pan_max || pos_pan < pos_pan_min || pos_tilt > pos_tilt_max || pos_tilt < pos_tilt_min)
                            {
                                Console.Write("***error![over moving range]***");
                                breakFlag = true;
                            }

                            //中断フラグtrueで処理から抜ける
                            if (breakFlag == true)
                            {
                                break;
                            }

                            //動作部分
                            Console.Write("num:" + count);                                        //カウント数の表示
                            Console.Write("   \tgrid:" + "(" + Xm + ", " + Ym + ")");             //グリッド[x,y]の表示
                            Console.Write("     \tPan:" + pos_pan + "   \tTilt:" + pos_tilt);     //角度[position]の表示
                            //Console.Write("\tPan:" + -deg_pan + "\tTilt:" + -deg_tilt);         //角度[degree]の表示
                            //Console.Write("\n");
                            //
                            port.Write("PP" + pos_pan + " ");       //***Panの動作角度入力***
                            port.Write("TP" + pos_tilt + " ");      //***Tiltの動作角度入力***
                            port.Write("A ");                       //コマンド実行
                            mre.Reset();                            //一回停止させる
                            mre.WaitOne();
                            port.Write("MS ");                      //時間取得用フラグ（MS : Monitor status）
                            //Thread.Sleep(20);                       //***仮停止時間(0.01sec)***（タイムスタンプのシリアル通信遅延のため）
                            Thread.Sleep(1000);                   //***停止時間(1sec)***
                            //
                            count++;                     //計測回数カウント
                        }////////////////////////////////////////////////////////////////////////////////////////////

                        if (Y_loop + DELTA <= YMAX)  //帰りがけできるかの確認
                        {
                            Y_loop = Y_loop + DELTA;            //y方向に+計測幅
                            for (X_loop = XMAX; X_loop >= XMIN; X_loop = X_loop - DELTA)  //x方向（横方向）のループ[帰りがけ：+から-]/////
                            {
                                Xm = X_loop / 100.0;  //計算用に[m]に直す
                                Ym = Y_loop / 100.0;
                                deg_pan = CalPan(Xm, Ym, length_pan);                           //ファイ(degree)の取得
                                deg_tilt = CalTilt(Xm, Ym, Height, length_tilt, length_pan);    //シータ(degree)の取得
                                pos_pan = -deg_pan / degpos;        //Pan-potitionへの変換:-かける
                                pos_tilt = -deg_tilt / degpos;      //Tilt-positionへの変換:-かける
                                pos_pan = Math.Round(pos_pan);      //数値の丸め（四捨五入）
                                pos_tilt = Math.Round(pos_tilt);
                                PAN = pos_pan.ToString();           //CSV書き込みPAN
                                TILT = pos_tilt.ToString();         //CSV書き込みTILT

                                //中断ボタン確認：qキーが押されれば中断する
                                if (_keyReaded)
                                {
                                    Console.WriteLine("qキーが押されたので中断します");
                                    breakFlag = true;
                                }

                                //動作範囲確認：動作範囲を越えた場合中断する
                                if (pos_pan > pos_pan_max || pos_pan < pos_pan_min || pos_tilt > pos_tilt_max || pos_tilt < pos_tilt_min)
                                {
                                    Console.Write("***error![over moving range]***");
                                    breakFlag = true;
                                }

                                //中断フラグtrueで処理から抜ける
                                if (breakFlag == true)
                                {
                                    break;
                                }

                                //動作部分
                                Console.Write("num:" + count);                                        //カウント数の表示
                                Console.Write("   \tgrid:" + "(" + Xm + ", " + Ym + ")");             //グリッド[x,y]の表示
                                Console.Write("     \tPan:" + pos_pan + "   \tTilt:" + pos_tilt);     //角度[position]の表示
                                //Console.Write("\tPan:" + -deg_pan + "\tTilt:" + -deg_tilt);         //角度[degree]の表示
                                //Console.Write("\n");
                                //
                                port.Write("PP" + pos_pan + " ");       //***Panの動作角度入力***
                                port.Write("TP" + pos_tilt + " ");      //***Tiltの動作角度入力***
                                port.Write("A ");                         //コマンド実行
                                mre.Reset();                              //一回停止させる
                                mre.WaitOne();
                                port.Write("MS ");                        //移動完了フラグ（MS : Monitor status）
                                //Thread.Sleep(20);                         //***仮停止時間(0.01sec)***（タイムスタンプのシリアル通信遅延のため）
                                Thread.Sleep(1000);                     //***停止時間(1sec)***
                                //
                                count++;                     //計測回数カウント
                            }////////////////////////////////////////////////////////////////////////////////////////////

                        }

                        if (breakFlag == true)
                        {
                            break;
                        }
                    }
                    //実行終了時の初期化
                    Console.Write("initPanTilt\n"); //初期化
                    port.Write("PP00 ");
                    port.Write("TP00 ");
                    port.Write("A ");
                    mre.Reset();            //一回停止させる
                    mre.WaitOne();
                    EndprocessFlag = true;  //実行終了フラグtrue
                    Console.WriteLine("Finish！");

                    //END実行部（メインの流れ）********************************************************************
                },
                tokenSource.Token
            );
            try
            {
                task.Start();
                while (true)
                {

                    while (true)
                    {
                        if (AstarFlag == true)      //A*flagがtrueになったら再開
                        {
                            mre.Set();
                            AstarFlag = false;
                        }
                        if (EndprocessFlag == true) //処理がすべて終了すればブレイク
                        {
                            EndprocessFlag = false;
                            break;
                        }  
                    }
                    break;
                }
                tokenSource.Cancel();
                task.Wait();
            }
            finally
            {
                //task.Dispose();
            } 
        }
        //END実行部***************************************************************************************************


        //中断ボタン処理***************************************************************************************************
        volatile static bool _keyReaded = false;    //中断ボタンフラグ

        private static void interruption(object userState)
        {
            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
            if (keyInfo.KeyChar == 'q')
            {
                _keyReaded = true;
            }
        }
        //*****************************************************************************************************************


        //角度計算用関数***************************************************************************************************
        private static double CalPan(double x, double y, double A)         //Panの角度の計算(ファイ) Atan2版
        {
            double rad_phi, deg_phi, r, R;
            r = Math.Sqrt(x * x + y * y);
            R = Math.Sqrt(r * r - A * A);
            rad_phi = Math.PI / 2 - Math.Atan2(y, x) - Math.Atan2(A, R);
            deg_phi = rad_phi * 180.0 / Math.PI;
            return deg_phi;
        }

        private static double CalTilt(double x, double y, double H, double l, double A)  //Tiltの角度の計算(シータ) Atan2版
        {
            double rad_theta, deg_theta, r, RHl, R;
            r = Math.Sqrt(x * x + y * y);
            R = Math.Sqrt(r * r - A * A);
            RHl = Math.Sqrt(R * R + H * H - l * l);
            rad_theta = Math.Atan2(l, RHl) + Math.Atan2(H, R);
            deg_theta = rad_theta * 180.0 / Math.PI;
            return deg_theta;
        }
        //**********************************************************************************************************


        //接続終了***************************************************************************************************
        private static void Disconnect(SerialPort port)
        {
            port.Close();
            port.Dispose();
        }
        //END接続終了************************************************************************************************************

    }
}
