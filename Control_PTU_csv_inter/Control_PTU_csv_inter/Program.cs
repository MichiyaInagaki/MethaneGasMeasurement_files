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

namespace Control_PTU_csv_inter
{
    class Program
    {
        //初期設定//////////////////////////////////////////////////////////////////////////////////
        //CSVファイルパス
        private static string csv_path = @"C:\Users\SENS\source\repos\Control_PTU_csv_inter\Control_PTU_csv_inter\csv\test\";    //書き込み場所
        private static string read_csv_path = @"C:\Users\SENS\source\repos\Control_PTU_csv_inter\Control_PTU_csv_inter\csv\base_points.csv";    //読み込み場所
        private static string output_path;        //CSVファイル出力先のパス格納用
        //計測範囲の設定
        private static double _xmin = -1.0;
        private static double _xmax = 1.0;
        private static double _ymin = 0.6;
        private static double _ymax = 2.6;
        private static double _delta = 0.2;       //計測点の刻み幅
        private static double _delta_PTU = 0.2;   //PTUを置く位置の刻み幅
        //ベースポイントをx座標，y座標，パン角，チルト角に分けて取得
        private static List<string> str_x = new List<string>();
        private static List<string> str_y = new List<string>();
        private static List<string> str_pan = new List<string>();
        private static List<string> str_tilt = new List<string>();
        //END初期設定///////////////////////////////////////////////////////////////////////////////

        static void Main(string[] args)
        {
            //ベースポイント読み込み（CSV読み込み）*************************************************************
            StreamReader reader = new StreamReader(read_csv_path, Encoding.GetEncoding("Shift_JIS"));
            while (reader.Peek() >= 0)
            {
                string[] cols = reader.ReadLine().Split(',');
                str_x.Add(cols[0]);
                str_y.Add(cols[1]);
                str_pan.Add(cols[2]);
                str_tilt.Add(cols[3]);
            }
            reader.Close();
            //ENDベースポイント読み込み（CSV読み込み）*************************************************************

            //接続部********************************************************************************************
            SerialPort port = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);      //通信設定
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
                string rec_str = "receive" + str;
                //Console.Write("受信:" + rec_str+"\n");  //受信信号表示

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
                    Console.WriteLine("\tTime stamp: " + DT);            //タイムスタンプ***
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
                Console.WriteLine("fキー：フラグ時刻の取得 / sキー：実行 / qキー：中断 / dキー：接続終了 / cキー：キャリブレーション用");
                var key = Console.ReadKey(false);
                //接続終了------------------------------------------------------
                if (key.KeyChar == 'd')
                {
                    Disconnect(port);
                    Environment.Exit(0);    //コンソールアプリケーションの終了
                }

                //フラグ時刻の取得----------------------------------------------
                if (key.KeyChar == 'f')
                {
                    //時刻合わせ用タイムスタンプ
                    port.Write("I ");             //Immediare mode
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
                        using (var sw = new StreamWriter(csv_path + "Flag_time.csv", append))
                        {
                            sw.WriteLine(DT);  //書き込み
                        }
                    }
                    catch (Exception err)
                    {
                        // ファイルを開くのに失敗したときエラーメッセージを表示
                        Console.WriteLine(err.Message);
                    }
                    //////////////////////////////////////////////////////////////////////////////////////
                    port.Write("S ");              //Slaved mode
                }

                //キャリブレーション用----------------------------------------------
                if (key.KeyChar == 'c')
                {
                    //座標入力
                    Console.Write("X座標: ");
                    double Xp = double.Parse(Console.ReadLine());
                    Console.Write("Y座標: ");
                    double Yp = double.Parse(Console.ReadLine());
                    //高さ設定
                    double Height = 0.273 + 0.01 + 0.091;            //PTUの高さ[m]=移動ロボットの高さ+固定盤の厚み+PTUの腕関節までの長さ
                    double length_tilt = 0.038 + 0.01 + 0.02;        //Tilt部分の腕の長さ+取り付け具の厚み+メタン計の中心まで[m]
                    double length_pan = 0.019;                       //レーザーの発射口とファイ回転軸中心からのずれ[m]
                    //初期化
                    port.Write("S ");           //Slaved mode
                    port.Write("PS500 ");       //Pan速度設定
                    port.Write("TS500 ");       //Tilt速度設定
                    port.Write("PP00 ");        //Pan0
                    port.Write("TP00 ");        //Tilt0
                    port.Write("A ");
                    //角度計算
                    int pos_pan_max, pos_pan_min, pos_tilt_max, pos_tilt_min;
                    pos_pan_max = 3094;
                    pos_pan_min = -3095;
                    pos_tilt_max = 593;
                    pos_tilt_min = -908;
                    //
                    double resolition = 185.1429;       //分解能
                    double degpos = resolition / 3600;  //[degree/position]
                    double deg_pan, deg_tilt, pos_pan, pos_tilt;
                    //角度計算
                    deg_pan = CalPan(Xp, Yp, length_pan);                           //ファイ(degree)の取得
                    deg_tilt = CalTilt(Xp, Yp, Height, length_tilt, length_pan);    //シータ(degree)の取得
                    pos_pan = -deg_pan / degpos;        //Pan-potitionへの変換 :[[-かける]]
                    pos_tilt = -deg_tilt / degpos;      //Tilt-positionへの変換:[[-かける]]
                    pos_pan = Math.Round(pos_pan);      //数値の丸め（四捨五入）
                    pos_tilt = Math.Round(pos_tilt);
                    //Console.WriteLine("キャリブレーション>" + " X:" + Xp + " Y:" + Yp + " deg_pan:" + deg_pan + " deg_tilt:" + deg_tilt);
                    Console.WriteLine("キャリブレーション>" + " X:" + Xp + " Y:" + Yp + " pos_pan:" + pos_pan + " pos_tilt:" + pos_tilt);
                    //
                    //キャプション角度入力
                    Console.Write("Pan角: ");
                    double cal_input_pan = double.Parse(Console.ReadLine());
                    Console.Write("Tilt角: ");
                    double cal_input_tilt = double.Parse(Console.ReadLine());
                    //
                    //動作範囲確認：動作範囲を越えた場合中断する
                    if (cal_input_pan > pos_pan_max || cal_input_pan < pos_pan_min || cal_input_tilt > pos_tilt_max || cal_input_tilt < pos_tilt_min)
                    {
                        Console.WriteLine("***error![over moving range]***");
                    }
                    else
                    {
                        //実行
                        port.Write("PP" + cal_input_pan + " ");       //***Panの動作角度入力***
                        port.Write("TP" + cal_input_tilt + " ");      //***Tiltの動作角度入力***
                        port.Write("A ");                         //コマンド実行
                        Thread.Sleep(5000);                   //***停止時間(5sec)***
                    }
                    //
                    //初期化
                    port.Write("PP00 ");
                    port.Write("TP00 ");
                    port.Write("A ");
                    Thread.Sleep(1000);
                }

                //実行部へ----------------------------------------------------------------
                if (key.KeyChar == 's')
                {
                    ////////////////////////Exercute関数の仕様//////////////////////////////////////////
                    //Exercute(port, xmin, xmax, ymin, ymax, delta, output_path)                      //
                    //xmin, xmax, ymin, ymax: 測定の範囲（パンチルトの左右方向がx，前後方向がy）[m]   //
                    //delta: 測定の刻み幅[m]（!!測定範囲を割り切れるものにすること!!）                //
                    ////////////////////////////////////////////////////////////////////////////////////

                    double _xmin_temp = 0;      //左端から計測を始めます
                    double _xmax_temp = _xmax - _xmin;

                    string file_Path = csv_path + "MP" + exercute_num.ToString() + ".csv";  //出力先ファイルパス，ファイル名
                    double _xmin_temp2 = _xmin_temp - (exercute_num * _delta_PTU);      //計測点の左端をPTUを置く位置の刻み幅分だけずらしていく
                    double _xmax_temp2 = _xmax_temp - (exercute_num * _delta_PTU);      //計測点の右端をPTUを置く位置の刻み幅分だけずらしていく
                    if (_xmax_temp2 < 0)        //右は次まで来たら計測を終わります
                    {
                        Console.WriteLine("すべての計測が終わりました");
                    }
                    else
                    {
                        //関数の仕様：Exercute(port, xmin, xmax, ymin, ymax, delta, output_path)
                        Exercute(port, _xmin_temp2, _xmax_temp2, _ymin, _ymax, _delta, file_Path);    //実行部へ
                        exercute_num++;
                    }
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

        //実行回数フラグ***
        private static int exercute_num = 0;

        //実行部************************************************************************************************
        private static void Exercute(SerialPort port, double xmin, double xmax, double ymin, double ymax, double delta, string output)
        {
            List<int> base_pan = str_pan.ConvertAll(x => int.Parse(x));          //Pan角
            List<int> base_tilt = str_tilt.ConvertAll(x => int.Parse(x));        //Tilt角
            //計測範囲の記述
            Console.WriteLine("xmin:" + xmin + " xmax:" + xmax + " ymin:" + ymin + " ymax:" + ymax + " delta" + delta);
            //初期設定（計測範囲，最大角，変数設定）************************************************************
            int count = 1;                          //測定点の数カウント用
            int co_num = -1;                        //csvの行番号格納用
            ///////ループ用変数の設定///////////////////////////////////////////////
            int X_loop, Y_loop;                               //ループ用変数
            int XMIN, XMAX, YMIN, YMAX, DELTA;         //ループ用に[cm]に直す,int型にキャスト
            XMIN = (int)(xmin * 100);
            XMAX = (int)((xmax + 0.001) * 100);          //+0.001はキャスト問題解消のため:int(0.8*100)=79になる
            YMIN = (int)(ymin * 100);
            YMAX = (int)(ymax * 100);
            DELTA = (int)(delta * 100);
            ///////ロボットプラットフォームの寸法設定[m]////////////////////////////
            double Height = 0.273 + 0.01 + 0.091 - 0.02;             //PTUの高さ[m]=移動ロボットの高さ+固定盤の厚み+PTUの腕関節までの長さ
            double length_tilt = 0.038 + 0.01 + 0.02 - 0.01;        //Tilt部分の腕の長さ+取り付け具の厚み+メタン計の中心まで[m]
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
                    port.Write("PS1000 ");       //Pan速度設定
                    port.Write("TS1000 ");       //Tilt速度設定
                    port.Write("PP00 ");        //Pan0
                    port.Write("TP00 ");        //Tilt0
                    port.Write("A ");
                    mre.Reset();            //一回停止させる
                    mre.WaitOne();
                    //

                    for (Y_loop = YMIN; Y_loop <= YMAX; Y_loop = Y_loop + DELTA)      //y方向（縦方向）のループ
                    {
                        for (X_loop = XMIN; X_loop <= XMAX; X_loop += DELTA)  //x方向（横方向）のループ[行きがけ：-から+]/////////////
                        {
                            Xm = X_loop / 100.0;  //計算用にxy座標を[m]に直す
                            Ym = Y_loop / 100.0;
                            co_num = Getbasepoint(Xm,Ym);       //csvの行番号取得

                            deg_pan = CalPan(Xm, Ym, length_pan);                           //ファイ(degree)の取得
                            deg_tilt = CalTilt(Xm, Ym, Height, length_tilt, length_pan);    //シータ(degree)の取得
                            pos_pan = -deg_pan / degpos;        //Pan-potitionへの変換 :[[-かける]]
                            pos_tilt = -deg_tilt / degpos;      //Tilt-positionへの変換:[[-かける]]
                            pos_pan = Math.Round(pos_pan);      //数値の丸め（四捨五入）
                            pos_tilt = Math.Round(pos_tilt);
                            PAN = pos_pan.ToString();           //CSV書き込みPAN
                            TILT = pos_tilt.ToString();         //CSV書き込みTILT

                            pos_pan = base_pan[co_num];     //csvからのデータ読み込み
                            pos_tilt = base_tilt[co_num];
                            //PAN = str_pan[co_num];      //CSV書き込みPAN
                            //TILT = str_tilt[co_num];    //CSV書き込みTILT


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
                            Console.Write("   \tloop:" + "(" + X_loop + ", " + Y_loop + ", " + xmax + ", " + XMAX + ")");             //グリッド[x,y]の表示
                            Console.Write("   \tgrid:" + "(" + Xm + ", " + Ym + ")");             //グリッド[x,y]の表示
                            Console.Write("     \tPan:" + pos_pan + "   \tTilt:" + pos_tilt);     //角度[position]の表示
                            //Console.Write("\tPan:" + -deg_pan + "\tTilt:" + -deg_tilt);         //角度[degree]の表示
                            //Console.Write("\n");
                            //
                            //port.Write("PP" + pos_pan + " ");       //***Panの動作角度入力***
                            //port.Write("TP" + pos_tilt + " ");      //***Tiltの動作角度入力***
                            port.Write("A ");                       //コマンド実行
                            mre.Reset();                            //一回停止させる
                            mre.WaitOne();
                            port.Write("MS ");                      //時間取得用フラグ（MS : Monitor status）
                            Thread.Sleep(20);                       //***仮停止時間(0.01sec)***（タイムスタンプのシリアル通信遅延のため）
                            //Thread.Sleep(1000);                   //***停止時間(1sec)***
                            //
                            count++;                     //計測回数カウント
                        }////////////////////////////////////////////////////////////////////////////////////////////

                        if (Y_loop + DELTA <= YMAX)  //帰りがけできるかの確認
                        {
                            Y_loop = Y_loop + DELTA;            //y方向に+計測幅
                            for (X_loop = XMAX; X_loop >= XMIN; X_loop -= DELTA)  //x方向（横方向）のループ[帰りがけ：+から-]/////
                            {
                                Xm = X_loop / 100.0;  //計算用に[m]に直す
                                Ym = Y_loop / 100.0;
                                co_num = Getbasepoint(Xm, Ym);       //csvの行番号取得

                                deg_pan = CalPan(Xm, Ym, length_pan);                           //ファイ(degree)の取得
                                deg_tilt = CalTilt(Xm, Ym, Height, length_tilt, length_pan);    //シータ(degree)の取得
                                pos_pan = -deg_pan / degpos;        //Pan-potitionへの変換:-かける
                                pos_tilt = -deg_tilt / degpos;      //Tilt-positionへの変換:-かける
                                pos_pan = Math.Round(pos_pan);      //数値の丸め（四捨五入）
                                pos_tilt = Math.Round(pos_tilt);
                                PAN = pos_pan.ToString();           //CSV書き込みPAN
                                TILT = pos_tilt.ToString();         //CSV書き込みTILT

                                pos_pan = base_pan[co_num];     //csvからのデータ読み込み
                                pos_tilt = base_tilt[co_num];

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
                                Console.Write("   \tloop:" + "(" + X_loop + ", " + Y_loop + ", " + xmax + ", " + XMAX + ")");             //グリッド[x,y]の表示
                                Console.Write("   \tgrid:" + "(" + Xm + ", " + Ym + ")");             //グリッド[x,y]の表示
                                Console.Write("     \tPan:" + pos_pan + "   \tTilt:" + pos_tilt);     //角度[position]の表示
                                //Console.Write("\tPan:" + -deg_pan + "\tTilt:" + -deg_tilt);         //角度[degree]の表示
                                //Console.Write("\n");
                                //
                                //port.Write("PP" + pos_pan + " ");       //***Panの動作角度入力***
                                //port.Write("TP" + pos_tilt + " ");      //***Tiltの動作角度入力***
                                port.Write("A ");                         //コマンド実行
                                mre.Reset();                              //一回停止させる
                                mre.WaitOne();
                                port.Write("MS ");                        //移動完了フラグ（MS : Monitor status）
                                Thread.Sleep(20);                         //***仮停止時間(0.01sec)***（タイムスタンプのシリアル通信遅延のため）
                                //Thread.Sleep(1000);                     //***停止時間(1sec)***
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


        //ベースポイントの行番号取得関数
        private static int Getbasepoint(double xp, double yp)
        {
            int basepoint_num = -1;  //返り値の行番号
            //string型からのキャスト
            List<int> base_x = str_x.ConvertAll(x => int.Parse(x));               //x座標
            List<int> base_y = str_y.ConvertAll(x => int.Parse(x));               //y座標
            int length_csv = base_x.Count;    //リストの長さ取得

            //リストを検索して一致するのを見つける処理***
            int Xp;
            if (xp > 0)
            {
                Xp = (int)(xp * 100 + 0.001);    //比較のためint型にキャスト
            }
            else
            {
                Xp = (int)(xp * 100 - 0.001);    //比較のためint型にキャスト
            }
            int Yp = (int)(yp * 100 + 0.001);
            Console.WriteLine("Xp" + Xp + " Yp" + Yp);

            List<int> co_list = new List<int>();    //一致した行番号を格納するリスト
            int num = base_x.IndexOf(Xp);           //xpと一致するリストbase_xの最初の行番号
            co_list.Add(num);       //一致行番号リストに追加
            if (num >= 0)
            {
                // IndexOfメソッドで見つからなくなるまで繰り返す
                while (num >= 0)
                {
                    //見つかった位置の次の位置から検索
                    num = base_x.IndexOf(Xp, num + 1);
                    if (num >= 0)
                    {
                        co_list.Add(num);   //一致行番号リストに追加
                    }
                }
            }
            else
            {
                Console.WriteLine("一致するx座標がありません");
            }

            int length_co = co_list.Count;    //リストの長さ取得
            for (int i=0; i< length_co; i++)
            {
                if (base_y[co_list[i]] == Yp)
                {
                    basepoint_num = co_list[i];      //行番号取得
                    Console.WriteLine("一致行番号"+ co_list[i]);
                    break;
                }
            }
            if (basepoint_num == -1)
            {
                Console.WriteLine("一致するy座標がありません");
            }

            return basepoint_num;
        }


        //接続終了***************************************************************************************************
        private static void Disconnect(SerialPort port)
        {
            port.Close();
            port.Dispose();
        }
        //END接続終了************************************************************************************************************

    }
}
