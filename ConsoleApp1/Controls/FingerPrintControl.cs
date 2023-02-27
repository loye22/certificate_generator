using libzkfpcsharp;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZKTecoFingerPrintScanner_Implementation;
using ZKTecoFingerPrintScanner_Implementation.Helpers;
using Newtonsoft.Json;
using System.Configuration;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using CircularProgressBar;
using System.Reflection.Emit;
using ZKTecoFingerPrintScanner_Implementation.Properties ;

using Word = Microsoft.Office.Interop.Word;

namespace Dofe_Re_Entry.UserControls.DeviceController
{
    public partial class FingerPrintControl : UserControl
    {


        /// <summary>
        // this dic hold a pain of class name and there students 
        Dictionary <string, List<string>> dictionary = new Dictionary<string, List<string>>();
        List<traner> helperList;
        int rowCourser = 0;
        int limit = 0;
        String pathP;
        String companyName ="";

        

        /// </summary>

        const string VerifyButtonDefault = "Verify";
        const string VerifyButtonToggle = "Stop Verification";
        const string Disconnected = "Disconnected";

        Thread captureThread = null;

        public Master parentForm = null;

        #region -------- FIELDS --------

        const int REGISTER_FINGER_COUNT = 3;

        zkfp fpInstance = new zkfp();
        IntPtr FormHandle = IntPtr.Zero; // To hold the handle of the form
        bool bIsTimeToDie = false;
        bool IsRegister = false;
        bool bIdentify = true;
        byte[] FPBuffer;   // Image Buffer
        int RegisterCount = 0;

        byte[][] RegTmps = new byte[REGISTER_FINGER_COUNT][];

        byte[] RegTmp = new byte[2048];
        byte[] CapTmp = new byte[2048];
        int cbCapTmp = 2048;
        int regTempLen = 0;
        int iFid = 1;

        const int MESSAGE_CAPTURED_OK = 0x0400 + 6;


        

        private int mfpWidth = 0;
        private int mfpHeight = 0;


        #endregion


        // [ CONSTRUCTOR ]
        public  FingerPrintControl()
        {
            InitializeComponent();

            ReInitializeInstance();
         //   pBar.Value = 0;
            //timer1.Start();
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            initComboBoxItems();

           





        }

        public String getDatafun()
        {
            return "heyhey";
         
        }


        // [ INITALIZE DEVICE ]
        private void bnInit_Click(object sender, EventArgs e)
        {
            parentForm.statusBar.Visible = false;
            cmbIdx.Items.Clear();

            int initializeCallBackCode = fpInstance.Initialize();
            if (zkfp.ZKFP_ERR_OK == initializeCallBackCode)
            {
                int nCount = fpInstance.GetDeviceCount();
                if (nCount > 0)
                {
                    for (int i = 1; i <= nCount; i++) cmbIdx.Items.Add(i.ToString());

                    cmbIdx.SelectedIndex = 0;
                    btnInit.Enabled = false;

                    DisplayMessage(MessageManager.msg_FP_InitComplete, true);
                }
                else
                {
                    int finalizeCount = fpInstance.Finalize();
                    DisplayMessage(MessageManager.msg_FP_NotConnected, false);
                }




                // CONNECT DEVICE

                #region -------- CONNECT DEVICE --------

                int openDeviceCallBackCode = fpInstance.OpenDevice(cmbIdx.SelectedIndex);
                if (zkfp.ZKFP_ERR_OK != openDeviceCallBackCode)
                {
                    DisplayMessage($"Uable to connect with the device! (Return Code: {openDeviceCallBackCode} )", false);
                    return;
                }

                Utilities.EnableControls(false, btnInit);
                Utilities.EnableControls(true, btnClose, btnEnroll, btnVerify, btnIdentify, btnFree);

                RegisterCount = 0;
                regTempLen = 0;
                iFid = 1;

                //for (int i = 0; i < 3; i++)
                for (int i = 0; i < REGISTER_FINGER_COUNT; i++)
                {
                    RegTmps[i] = new byte[2048];
                }

                byte[] paramValue = new byte[4];
                int size = 4;

                //fpInstance.GetParameters

                fpInstance.GetParameters(1, paramValue, ref size);
                zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

                size = 4;
                fpInstance.GetParameters(2, paramValue, ref size);
                zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

                FPBuffer = new byte[mfpWidth * mfpHeight];

                //FPBuffer = new byte[fpInstance.imageWidth * fpInstance.imageHeight];

                captureThread = new Thread(new ThreadStart(DoCapture));
                captureThread.IsBackground = true;
                captureThread.Start();


                bIsTimeToDie = false;

                string devSN = fpInstance.devSn;
                lblDeviceStatus.Text = "Connected \nDevice S.No: " + devSN;

                DisplayMessage("You are now connected to the device.", true);



                #endregion

            }
            else
                DisplayMessage("Unable to initailize the device. " + FingerPrintDeviceUtilities.DisplayDeviceErrorByCode(initializeCallBackCode) + " !! ", false);

        }



        // [ CAPTURE FINGERPRINT ]
        private void DoCapture()
        {
           // Properties.
            try
            {
                while (!bIsTimeToDie)
                {
                    cbCapTmp = 2048;
                    int ret = fpInstance.AcquireFingerprint(FPBuffer, CapTmp, ref cbCapTmp);

                    if (ret == zkfp.ZKFP_ERR_OK)
                    {
                        //if (RegisterCount == 0)
                        //    btnEnroll.Invoke((Action)delegate
                        //    {
                        //        btnEnroll.Enabled = true;
                        //    });
                        SendMessage(FormHandle, MESSAGE_CAPTURED_OK, IntPtr.Zero, IntPtr.Zero);
                    }
                    Thread.Sleep(100);
                }
            }
            catch { }

        }

        [DllImport("user32.dll", EntryPoint = "SendMessageA")]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);


        private void bnIdentify_Click(object sender, EventArgs e)
        {
            if (!bIdentify)
            {
                bIdentify = true;
                DisplayMessage(MessageManager.msg_FP_PressForIdentification, true);
            }
        }

        private void bnVerify_Click(object sender, EventArgs e)
        {
            if (bIdentify)
            {
                bIdentify = false;
                btnVerify.Text = VerifyButtonToggle;
                DisplayMessage(MessageManager.msg_FP_PressForVerification, true);
            }
            else
            {
                bIdentify = true;
                btnVerify.Text = VerifyButtonDefault;
            }
        }


        protected override void DefWndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case MESSAGE_CAPTURED_OK:
                    {
                        parentForm.statusBar.Visible = false;
                        DisplayFingerPrintImage();

                        if (IsRegister)
                        {
                            #region -------- IF REGISTERED FINGERPRINT --------

                            int ret = zkfp.ZKFP_ERR_OK;
                            int fid = 0, score = 0;
                            ret = fpInstance.Identify(CapTmp, ref fid, ref score);
                            if (zkfp.ZKFP_ERR_OK == ret)
                            {
                                int deleteCode = fpInstance.DelRegTemplate(fid);   // <---- REMOVE FINGERPRINT
                                if (deleteCode != zkfp.ZKFP_ERR_OK)
                                {
                                    DisplayMessage(MessageManager.msg_FP_CurrentFingerAlreadyRegistered + fid, false);
                                    return;
                                }
                            }
                            if (RegisterCount > 0 && fpInstance.Match(CapTmp, RegTmps[RegisterCount - 1]) <= 0)
                            {
                                DisplayMessage("Please press the same finger " + REGISTER_FINGER_COUNT + " times for enrollment", true);

                                return;
                            }
                            Array.Copy(CapTmp, RegTmps[RegisterCount], cbCapTmp);


                            if (RegisterCount == 0) btnEnroll.Enabled = false;

                            RegisterCount++;
                            if (RegisterCount >= REGISTER_FINGER_COUNT)
                            {

                                RegisterCount = 0;
                                ret = GenerateRegisteredFingerPrint();   // <--- GENERATE FINGERPRINT TEMPLATE

                                if (zkfp.ZKFP_ERR_OK == ret)
                                {

                                    ret = AddTemplateToMemory();        //  <--- LOAD TEMPLATE TO MEMORY
                                    if (zkfp.ZKFP_ERR_OK == ret)         // <--- ENROLL SUCCESSFULL
                                    {
                                        string fingerPrintTemplate = string.Empty;
                                        zkfp.Blob2Base64String(RegTmp, regTempLen, ref fingerPrintTemplate);

                                        Utilities.EnableControls(true, btnVerify, btnIdentify);
                                        Utilities.EnableControls(false, btnEnroll);


                                        // GET THE TEMPLATE HERE : fingerPrintTemplate


                                        DisplayMessage(MessageManager.msg_FP_EnrollSuccessfull, true);

                                        DisconnectFingerPrintCounter();
                                    }
                                    else
                                        DisplayMessage(MessageManager.msg_FP_FailedToAddTemplate, false);

                                }
                                else
                                    DisplayMessage(MessageManager.msg_FP_UnableToEnrollCurrentUser + ret, false);

                                IsRegister = false;
                                return;
                            }
                            else
                            {
                                int remainingCont = REGISTER_FINGER_COUNT - RegisterCount;
                                lblFingerPrintCount.Text = remainingCont.ToString();
                                string message = "Please provide your fingerprint " + remainingCont + " more time(s)";

                                DisplayMessage(message, true);

                            }
                            #endregion
                        }
                        else
                        {

                            #region ------- IF RANDOM FINGERPRINT -------
                            // If unidentified random fingerprint is applied

                            if (regTempLen <= 0)
                            {
                                DisplayMessage(MessageManager.msg_FP_UnidentifiedFingerPrint, false);
                                return;
                            }


                            if (bIdentify)
                            {
                                int ret = zkfp.ZKFP_ERR_OK;
                                int fid = 0, score = 0;
                                ret = fpInstance.Identify(CapTmp, ref fid, ref score);
                                if (zkfp.ZKFP_ERR_OK == ret)
                                {
                                    DisplayMessage(MessageManager.msg_FP_IdentificationSuccess + ret, true);
                                    return;
                                }
                                else
                                {
                                    DisplayMessage(MessageManager.msg_FP_IdentificationFailed + ret, false);
                                    return;
                                }
                            }
                            else
                            {
                                int ret = fpInstance.Match(CapTmp, RegTmp);
                                if (0 < ret)
                                {
                                    DisplayMessage(MessageManager.msg_FP_MatchSuccess + ret, true);
                                    return;
                                }
                                else
                                {
                                    DisplayMessage(MessageManager.msg_FP_MatchFailed + ret, false);
                                    return;
                                }
                            }
                            #endregion
                        }
                    }
                    break;

                default:
                    base.DefWndProc(ref m);
                    break;
            }
        }



        /// <summary>
        /// FREE RESOURCES
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bnFree_Click(object sender, EventArgs e)
        {
            int result = fpInstance.Finalize();

            if (result == zkfp.ZKFP_ERR_OK)
            {
                DisconnectFingerPrintCounter();
                IsRegister = false;
                RegisterCount = 0;
                regTempLen = 0;
                ClearImage();
                cmbIdx.Items.Clear();
                Utilities.EnableControls(true, btnInit);
                Utilities.EnableControls(false, btnFree, btnClose, btnEnroll, btnVerify, btnIdentify);

                DisplayMessage("Resources were successfully released from the memory !!", true);
            }
            else
                DisplayMessage("Failed to release the resources !!", false);
        }

        private void ClearImage()
        {
            picFPImg.Image = null;
            //pbxImage2.Image = null;
        }

        private void bnEnroll_Click(object sender, EventArgs e)
        {
            if (!IsRegister)
            {
                ClearImage();
                IsRegister = true;
                RegisterCount = 0;
                regTempLen = 0;
                Utilities.EnableControls(false, btnEnroll, btnVerify, btnIdentify);
                DisplayMessage("Please press your finger " + REGISTER_FINGER_COUNT + " times to register", true);

                lblFingerPrintCount.Visible = true;
                lblFingerPrintCount.Text = REGISTER_FINGER_COUNT.ToString();
            }
        }




        public object PushToDevice(object args)
        {
            DisplayMessage("Pushed to fingerprint !", true);
            return null;
        }


        public void ReEnrollUser(bool enableEnroll, bool clearDeviceUser = true)
        {
            ClearImage();
            if (clearDeviceUser && !btnInit.Enabled) ClearDeviceUser();
            if (enableEnroll) btnEnroll.Enabled = true;
        }


        public void ClearDeviceUser()
        {
            try
            {
                int deleteCode = fpInstance.DelRegTemplate(iFid);   // <---- REMOVE FINGERPRINT
                if (deleteCode != zkfp.ZKFP_ERR_OK)
                {
                    DisplayMessage(MessageManager.msg_FP_UnableToDeleteFingerPrint + iFid, false);
                }
                iFid = 1;
            }
            catch { }

        }


        public bool ReleaseResources()
        {
            try
            {
                ReEnrollUser(true, true);
                bnClose_Click(null, null);
                return true;
            }
            catch
            {
                return false;
            }

        }

        #region -------- CONNECT/DISCONNECT DEVICE --------



        /// <summary>
        /// DISCONNECT DEVICE
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bnClose_Click(object sender, EventArgs e)
        {
            OnDisconnect();
        }


        public void OnDisconnect()
        {
            bIsTimeToDie = true;
            RegisterCount = 0;
            DisconnectFingerPrintCounter();
            ClearImage();
            Thread.Sleep(1000);
            int result = fpInstance.CloseDevice();

            captureThread.Abort();
            if (result == zkfp.ZKFP_ERR_OK)
            {
                Utilities.EnableControls(false, btnInit, btnClose, btnEnroll, btnVerify, btnIdentify);

                lblDeviceStatus.Text = Disconnected;

                Thread.Sleep(1000);
                result = fpInstance.Finalize();   // CLEAR RESOURCES

                if (result == zkfp.ZKFP_ERR_OK)
                {
                    regTempLen = 0;
                    IsRegister = false;
                    cmbIdx.Items.Clear();
                    Utilities.EnableControls(true, btnInit);
                    Utilities.EnableControls(false, btnClose, btnEnroll, btnVerify, btnIdentify);

                    ReInitializeInstance();

                    DisplayMessage(MessageManager.msg_FP_Disconnected, true);
                }
                else
                    DisplayMessage(MessageManager.msg_FP_FailedToReleaseResources, false);


            }
            else
            {
                string errorMessage = FingerPrintDeviceUtilities.DisplayDeviceErrorByCode(result);
                DisplayMessage(errorMessage, false);
            }
        }


        #endregion



        #region ------- COMMON --------

        private void FingerPrintControl_Load(object sender, EventArgs e) { FormHandle = this.Handle; }

        private void ReInitializeInstance()
        {
            Utilities.EnableControls(true, btnInit);
            Utilities.EnableControls(false, btnClose, btnEnroll, btnVerify, btnIdentify);
            DisconnectFingerPrintCounter();
            bIdentify = true;
            btnVerify.Text = VerifyButtonDefault;
        }

        private void DisconnectFingerPrintCounter()
        {
            lblFingerPrintCount.Text = REGISTER_FINGER_COUNT.ToString();
            lblFingerPrintCount.Visible = false;
        }

        #endregion


        #region -------- UTILITIES --------


        /// <summary>
        /// Combines Three Pre-Registered Fingerprint Templates as One Registered Fingerprint Template
        /// </summary>
        /// <returns></returns>
        private int GenerateRegisteredFingerPrint()
        {
            return fpInstance.GenerateRegTemplate(RegTmps[0], RegTmps[1], RegTmps[2], RegTmp, ref regTempLen);
        }

        /// <summary>
        /// Add A Registered Fingerprint Template To Memory | params: (FingerPrint ID, Registered Template)
        /// </summary>
        /// <returns></returns>
        private int AddTemplateToMemory()
        {
            return fpInstance.AddRegTemplate(iFid, RegTmp);
        }




        private void DisplayFingerPrintImage()
        {
            // NORMAL METHOD >>>

            //Bitmap fingerPrintImage = Utilities.GetImage(FPBuffer, fpInstance.imageWidth, fpInstance.imageHeight);
            //Rectangle cropRect = new Rectangle(0, 0, pbxImage2.Width / 2, pbxImage2.Height / 2);
            //Bitmap target = new Bitmap(cropRect.Width, cropRect.Height);
            //using (Graphics g = Graphics.FromImage(target))
            //{
            //    g.DrawImage(fingerPrintImage, new Rectangle(0, 0, target.Width, target.Height), cropRect, GraphicsUnit.Pixel);
            //}
            //this.pbxImage2.Image = target;



            // OPTIMIZED METHOD
            MemoryStream ms = new MemoryStream();
            BitmapFormat.GetBitmap(FPBuffer, mfpWidth, mfpHeight, ref ms);
            Bitmap bmp = new Bitmap(ms);
            this.picFPImg.Image = bmp;

        }

        private void DisplayMessage(string message, bool normalMessage)
        {
            try
            {
                Utilities.ShowStatusBar(message, parentForm.statusBar, normalMessage);
            }
            catch (Exception ex)
            {
                Utilities.ShowStatusBar(ex.Message, parentForm.statusBar, false);
            }
        }



        #endregion

        private void circularProgressBar1_Click(object sender, EventArgs e)
        {
           // pBar.  .ProgressBarStyle = ProgressBarStyle.Marquee;


           // timer1.Start();
     
        }

        private void timer1_Tick(object sender, EventArgs e )
        {
            if (pBar.Value < 100)
            {
              //  pBar.Value += 1;
               // label1.Text = pBar.Value.ToString() + "%";
            }

            else
            {
            //    timer1.Stop();

             //   MessageBox.Show("Pronto");
            }

            }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {

            String h = textBox2.Text.Replace(" ", "");
            String w = textBox4.Text.Replace(" ", "");

            int inputH;
            int inputW;

            if (h == "")
            {
                MessageBox.Show("Please enter the height ");
                return;
            }
            if (w == "")
            {
                MessageBox.Show("Please enter the weight ");
                return;
            }

            try
            {
                inputH = int.Parse(h);
                inputW = int.Parse(w);
                // label21.Text = (CalculateBMI(inputW, inputH) ).ToString().Substring(0, 4);
                label21.Text = (CalculateBMI(double.Parse(textBox4.Text), double.Parse(textBox2.Text)) * 10000).ToString().Substring(0, 4);


            }
            catch
            {
                MessageBox.Show("invaled input");
                return;

            }




        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            

        }



        ////////////////////////////////// louie funtions here  ///////////////////////////////////////////
        private void init11()
        {
            
                this. parentForm.statusBar.Visible = false;
                cmbIdx.Items.Clear();

                int initializeCallBackCode = fpInstance.Initialize();
                if (zkfp.ZKFP_ERR_OK == initializeCallBackCode)
                {
                    int nCount = fpInstance.GetDeviceCount();
                    if (nCount > 0)
                    {
                        for (int i = 1; i <= nCount; i++) cmbIdx.Items.Add(i.ToString());

                        cmbIdx.SelectedIndex = 0;
                        btnInit.Enabled = false;

                        DisplayMessage(MessageManager.msg_FP_InitComplete, true);
                    }
                    else
                    {
                        int finalizeCount = fpInstance.Finalize();
                        DisplayMessage(MessageManager.msg_FP_NotConnected, false);
                    }


                    // CONNECT DEVICE

                    #region -------- CONNECT DEVICE --------

                    int openDeviceCallBackCode = fpInstance.OpenDevice(cmbIdx.SelectedIndex);
                    if (zkfp.ZKFP_ERR_OK != openDeviceCallBackCode)
                    {
                        DisplayMessage($"Uable to connect with the device! (Return Code: {openDeviceCallBackCode} )", false);
                        return;
                    }

                    Utilities.EnableControls(false, btnInit);
                    Utilities.EnableControls(true, btnClose, btnEnroll, btnVerify, btnIdentify, btnFree);

                    RegisterCount = 0;
                    regTempLen = 0;
                    iFid = 1;

                    //for (int i = 0; i < 3; i++)
                    for (int i = 0; i < REGISTER_FINGER_COUNT; i++)
                    {
                        RegTmps[i] = new byte[2048];
                    }

                    byte[] paramValue = new byte[4];
                    int size = 4;

                    //fpInstance.GetParameters

                    fpInstance.GetParameters(1, paramValue, ref size);
                    zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

                    size = 4;
                    fpInstance.GetParameters(2, paramValue, ref size);
                    zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

                    FPBuffer = new byte[mfpWidth * mfpHeight];

                    //FPBuffer = new byte[fpInstance.imageWidth * fpInstance.imageHeight];

                    captureThread = new Thread(new ThreadStart(DoCapture));
                    captureThread.IsBackground = true;
                    captureThread.Start();


                    bIsTimeToDie = false;

                    string devSN = fpInstance.devSn;
                    lblDeviceStatus.Text = "Connected \nDevice S.No: " + devSN;

                    DisplayMessage("You are now connected to the device.", true);



                    #endregion

                }
                else
                    DisplayMessage("Unable to initailize the device. " + FingerPrintDeviceUtilities.DisplayDeviceErrorByCode(initializeCallBackCode) + " !! ", false);

            
        }

        private void inirolle()
        {
            if (!IsRegister)
            {
                ClearImage();
                IsRegister = true;
                RegisterCount = 0;
                regTempLen = 0;
                Utilities.EnableControls(false, btnEnroll, btnVerify, btnIdentify);
                DisplayMessage("Please press your finger " + REGISTER_FINGER_COUNT + " times to register", true);

                lblFingerPrintCount.Visible = true;
                lblFingerPrintCount.Text = REGISTER_FINGER_COUNT.ToString();
            }

        }

        bool initDev = false ;

        private void clear()
        {
            //dataGridView1.Rows.Clear();
            label10.Text = "Loading";
            label16.Text = "Loading";
            textBox6.Text = "Loading";
            label11.Text = "Loading";
            label14.Text = "Loading";
            label13.Text = "Loading";
            textBox2.Text = "Loading";
            textBox4.Text = "Loading";
            label23.Text = "Loading";
            ClearImage();

        }


        public static double CalculateBMI(double weight, double height)
        {
            return (weight / (height * height))   ;
        }


        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                if (!initDev)
                {
                     init11();
                  //inirolle();
                  initDev = true;
                    return; 

                }


                else
                {
                    // the device is inialized and redry to function 
                    if(label10.Text.ToLower() == "loading".ToLower())
                    {
                        MessageBox.Show("Please load the data first");
                        return; 

                    }

                    if (comboBox2.SelectedIndex == -1 )
                    {

                        MessageBox.Show("Please select the company");
                        return;


                    }

                    if (picFPImg.Image == null)
                    {
                        MessageBox.Show(" PLease scan you finger ");
                        return;

                    }
                    //String Bp = textBox2.Text.Replace(" ", "");
                    if (textBox1.Text.Replace(" ", "") == "")
                    {
                        MessageBox.Show(" PLease enter the BP ");
                        return;

                    }

                    // 1. save the image of finger print  

                    using (Bitmap bmp = new Bitmap(picFPImg.ClientSize.Width,
                             picFPImg.ClientSize.Height))
                    {

                        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                        // Create a new directory on the desktop

                        string folderName = "PICS";
                        string pathString = Path.Combine(desktopPath, folderName);
                        Directory.CreateDirectory(pathString);
                        pathString = Path.Combine(desktopPath, folderName);

                        string imagePath = Path.Combine(pathString, label10.Text + "_" + comboBox1.SelectedItem.ToString());
                        this.pathP = Path.Combine(pathString, label10.Text + "_" + comboBox1.SelectedItem.ToString());
                        picFPImg.DrawToBitmap(bmp, picFPImg.ClientRectangle);
                        bmp.Save(imagePath + ".jpg");
                    }

                    //MessageBox.Show("Captured successfully");


                  //  MessageBox.Show(this.comboBox2.SelectedText);


                    // export to PDF 
                    string p1 = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    String path = Path.Combine(p1, "d.docx");
                    var wordApp = new Word.Application(); //Application();
                    var wordDoc = wordApp.Documents.Add(path);

                    wordDoc.Content.Find.Execute(FindText: "{course}", ReplaceWith: label13.Text);
                    wordDoc.Content.Find.Execute(FindText: "{name}", ReplaceWith: label16.Text);
                    wordDoc.Content.Find.Execute(FindText: "{date}", ReplaceWith: "blabla");
                    wordDoc.Content.Find.Execute(FindText: "{company}", ReplaceWith:this.companyName);
                    wordDoc.Content.Find.Execute(FindText: "{id}", ReplaceWith: label10.Text);
                    wordDoc.Content.Find.Execute(FindText: "{height}", ReplaceWith: textBox2.Text);
                    wordDoc.Content.Find.Execute(FindText: "{weight}", ReplaceWith: textBox4.Text);
                    wordDoc.Content.Find.Execute(FindText: "{bp}", ReplaceWith: textBox1.Text);
                    wordDoc.Content.Find.Execute(FindText: "{na}", ReplaceWith: label14.Text);
                    wordDoc.Content.Find.Execute(FindText: "{gen}", ReplaceWith: label11.Text);
                    wordDoc.Content.Find.Execute(FindText: "{dob}", ReplaceWith: label23.Text);
                    wordDoc.Content.Find.Execute(FindText: "{bmi}", ReplaceWith: label21.Text);



                    string timestamp = DateTime.Now.Ticks.ToString();
                    string uniqueNumber = timestamp.Substring(0, 10);

                    wordDoc.Content.Find.Execute(FindText: "{ref}", ReplaceWith: uniqueNumber);

                    var shape = wordDoc.Shapes.AddPicture(
                        FileName: this.pathP + ".jpg",
                        LinkToFile: false,
                        SaveWithDocument: true,
                        Left: wordApp.InchesToPoints(4.34f),
                        Top: wordApp.InchesToPoints(8.20f),
                        Width: wordApp.InchesToPoints(1),
                        Height: wordApp.InchesToPoints(1)
                    );


                    wordDoc.SaveAs2(this.pathP + ".pdf", Word.WdSaveFormat.wdFormatPDF);
                    wordDoc.Close();
                    wordApp.Quit();
                    

                    ////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    MessageBox.Show("Done");
                    clear();



                }

            }

            catch(Exception e1)
            {
                MessageBox.Show("ErRor please take photo of this msg and send it to louie -->  " + e1.Message);
            }
        }

        public static bool CheckForInternetConnection(int timeoutMs = 10000, string url = null)
        {
            try
            {
                Ping myPing = new Ping();
                String host = "google.com";
                byte[] buffer = new byte[32];
                int timeout = 1000;
                PingOptions pingOptions = new PingOptions();
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                return (reply.Status == IPStatus.Success);
            }
            catch (Exception)
            {
                return false;
            }
        }




        public static async Task<string> GetClassesData()
        {
            string content = null;
            /*
            url = "https://www.tsti.ae/version-live/api/1.1/obj/new_request/?constraints=[ { \"key\": \"empid\", \"constraint_type\": \"equals\", \"value\": \"" + param1 + "\" }]";
            ;*/
            String url = "https://www.tsti.ae/version-live/api/1.1/obj/classes/?constraints=[ { \"key\": \"Status\", \"constraint_type\": \"equals\", \"value\": \"Ongoing\" }]";

            var client = new HttpClient();
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
              //  MessageBox.Show("het alomost there ");
                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }


        public static async Task<string> GetStudentData(String id )
        {
            string content = null;
            /*
            url = "https://www.tsti.ae/version-live/api/1.1/obj/new_request/?constraints=[ { \"key\": \"empid\", \"constraint_type\": \"equals\", \"value\": \"" + param1 + "\" }]";
            ;*/
            String url = "https://www.tsti.ae/version-live/api/1.1/obj/new_request/" + id;

            var client = new HttpClient();
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                //  MessageBox.Show("het alomost there ");
                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }



        public static async Task<string> GetCompanyData(String cID)
        {
            string content = null;

            String url = "https://www.tsti.ae/version-test/api/1.1/obj/companies/" + cID;

            var client = new HttpClient();
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {

                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }


        class traner
        {
            public String id;
            public String name;
            public String nationlity;
            public String course;
            public String empId;
            public String dob;
            public String gender;
            public String weight;
            public String height;
            public String cID;
            public traner(String a , String b , String c  , String d  , String f , String g , String h  , String i ,String ccompanyID )

            {
                this.id = a;
                this.name = b;  
                this.nationlity = c;        
                this.course = d;    
               
                this.dob = f;
                this.gender = g;
                this.weight = h;
                this.height = i;
                this.cID = ccompanyID;
                
               


 


                /*
       "empid"
      "name en"
      "gender"
       "nationality"
      "course"
      "weight"
        "height"
      "dob"

       */

            }


            public String getId()
            {
                return id;
            }

        }


        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        

        bool classDatainit = false;
        private async void button2_Click(object sender, EventArgs e)
        {
            if (!CheckForInternetConnection())
            {
                MessageBox.Show("Please check you connection and try again");
                return;
            }
            try
            {


                if (!classDatainit)
                {

                    pBar.Visible = true;
                    timer1.Start();


                    // MessageBox.Show("s");
                    var req = await GetClassesData();
                    var dynamicResult2 = JsonConvert.DeserializeObject<dynamic>(req);
                    int count = dynamicResult2.response.count;
                    List<String> myList; 
                    

                    for (int i = 0; i < count; i++)
                    {
                        String key = dynamicResult2.response.results[i]["title"].ToString();
                        myList = JsonConvert.DeserializeObject<List<string>>(dynamicResult2.response.results[i]["new request"].ToString());
                        this.dictionary.Add(key,myList);
                        comboBox1.Items.Add(key);
                    }
                    comboBox1.SelectedIndexChanged += new EventHandler(ComboBox_SelectedIndexChanged);
                    /*
                    foreach (KeyValuePair<string, List<string>> pair in this.dictionary)
                    {
                        Console.WriteLine("->>>>>" + pair.Key  + "   " + pair.Value[0]);
                    
                    }
                    */
                    timer1.Stop();
                    pBar.Visible = false;
                    MessageBox.Show("initialize successfully");
                    classDatainit = true;
                  
                }

                else
                {





                  
                    if (this.rowCourser != this.limit)
                    {

                        String companyName ="ss";

                        /*
                        try   
                        {//////////////// un comment this try/catch for the just for the demo 
                            MessageBox.Show(this.helperList[this.rowCourser].cID);
                            Console.WriteLine(this.helperList[this.rowCourser].cID);
                            var x = await GetCompanyData(this.helperList[this.rowCourser].cID);
                            var dR = JsonConvert.DeserializeObject<dynamic>(x);
                            //MessageBox.Show(dR.ToString());
                            companyName = dR.response["name"].ToString();
                            //MessageBox.Show(companyName);

                        }
                        catch (Exception eee)
                        {
                            MessageBox.Show("ErroR  " +  eee.Message);
                            Console.WriteLine("ErroR  " + eee.Message);

                        }
                        */  
                        
                        label10.Text = this.helperList[this.rowCourser].id; 
                        label16.Text= this.helperList[this.rowCourser].name;
                        textBox6.Text  = this.helperList[this.rowCourser].gender;
                        label11.Text = this.helperList[this.rowCourser].gender;
                        label14.Text = this.helperList[this.rowCourser].nationlity;
                        label13.Text = this.helperList[this.rowCourser].course;
                        textBox2.Text = this.helperList[this.rowCourser].height;
                        textBox4.Text = this.helperList[this.rowCourser].weight;
                        textBox3.Text = companyName;
                        label23.Text = this.helperList[this.rowCourser].dob;
                        label18.Text = companyName;
                        // textBox5.Text = (CalculateBMI(inputW, inputH) / 1000).ToString().Substring(0, 4);
                        //double.Parse(textBox4.Text), double.Parse(textBox2.Text) 
                       // MessageBox.Show(CalculateBMI(double.Parse(textBox4.Text), double.Parse(textBox2.Text)).ToString());
                        label21.Text = (CalculateBMI(double.Parse(textBox4.Text), double.Parse(textBox2.Text)) * 10000).ToString().Substring(0, 4);
                        dataGridView1.Rows[this.rowCourser].DefaultCellStyle.BackColor = Color.Yellow;
                     //   this.rowCourser++;
                    //    MessageBox.Show(this.rowCourser.ToString() + "   " + this.limit.ToString());
               
                    }

                    /* 
                     if (this.rowCourser == this.limit)
                     {
                         this.rowCourser = 0;
                         return;

                     }
               */
                    //  dataGridView1.Rows[rowIndex].Cells["id"].Value = trainer.id;
                    if (dataGridView1.Rows[0].Cells["id"].Value == null)
                    {
                        // the table is empty
                        MessageBox.Show("Please select the class first");
                        return; 
                    }
                   

                    if (rowCourser == 0)
                    {
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.served = "loading";
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.now = this.helperList[rowCourser].name.ToString();
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.next = this.helperList[rowCourser + 1].name;
                        this.rowCourser++;
                        return;
                    }
                    if (rowCourser + 1  == this.helperList.Count)
                    {
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.served = this.helperList[rowCourser - 1].name;
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.now = this.helperList[rowCourser].name;
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.next = "loading";
                        this.rowCourser++;
                        return;
                    }
                    else
                    {
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.served = this.helperList[rowCourser - 1].name;
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.now = this.helperList[rowCourser].name;
                        ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.next = this.helperList[rowCourser + 1].name;
                        this.rowCourser++;
                        //return; 
                    }

                   

                }




                




            }
            catch (IndexOutOfRangeException ex )
            {
                MessageBox.Show("The class over allready plz select new class");
                
            }

            catch (Exception e2) {
                MessageBox.Show("plz take pic and send it to tech team ->  " , e2.Message.ToString() );
                Console.WriteLine(e2.Message);
            }
        }

 

       // comboBox.SelectedIndexChanged += new EventHandler(ComboBox_SelectedIndexChanged);

        // define the event handler method
        private async void ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                dataGridView1.Rows.Clear();
                if (!CheckForInternetConnection())
                {
                    MessageBox.Show("Please check you connection and try again");
                    return;
                }

              //  dataGridView1.ClearSelection();
                List<traner> tranersList = new List<traner>();
                string selectedItem = comboBox1.SelectedItem.ToString();
                int count = dictionary[selectedItem].Count;

                pBar.Visible = true;
                timer1.Start();

                for (int i = 0; i < count; i++)
                {
                    var studentJsonData = await GetStudentData(dictionary[selectedItem][i].ToString());
                    var dynamicResult4 = JsonConvert.DeserializeObject<dynamic>(studentJsonData);
                    String a, b, c ,d , ee ,f,g,h , companyID;
                    a = dynamicResult4.response["empid"].ToString();
                    b = dynamicResult4.response["name en"].ToString();
                    c = dynamicResult4.response["nationality"].ToString(); 
                    d = dynamicResult4.response["course"].ToString(); 
                    ee = dynamicResult4.response["dob"].ToString();
                    f = dynamicResult4.response["gender"].ToString(); 
                    g = dynamicResult4.response["weight"].ToString();  
                    h = dynamicResult4.response["height"].ToString();
                    companyID = dynamicResult4.response["company"].ToString();
                    tranersList.Add(new traner(a,b,c,d,ee,f,g,h,companyID));
                    


                }
                timer1.Stop();
                pBar.Visible=false;
             // this.pBar.Value = 0; 
                this.helperList = tranersList;
             //   dataGridView1.Rows.Clear();

                foreach (var trainer in tranersList)
                {
                    int rowIndex = dataGridView1.Rows.Add();
                    dataGridView1.Rows[rowIndex].Cells["id"].Value = trainer.id;
                    dataGridView1.Rows[rowIndex].Cells["Name"].Value = trainer.name;
                    dataGridView1.Rows[rowIndex].Cells["nationlity"].Value = trainer.nationlity;
                    dataGridView1.Rows[rowIndex].Cells["course"].Value = trainer.course;
                   // dataGridView1.Rows[rowIndex].Cells["empId"].Value = trainer.empId;
                    dataGridView1.Rows[rowIndex].Cells["dob"].Value = trainer.dob;
                    dataGridView1.Rows[rowIndex].Cells["gender"].Value = trainer.gender;
                    dataGridView1.Rows[rowIndex].Cells["weight"].Value = trainer.weight;
                    dataGridView1.Rows[rowIndex].Cells["height"].Value = trainer.height;
                }



                //   MessageBox.Show(tranersList[0].id);

                /*
                 "empid"
                "name en"
                "gender"
                 "nationality"
                "course"
                "weight"
                  "height"
                "dob"

                 */
                this.rowCourser = 0;
                this.limit = dictionary[selectedItem].Count; 
              //  MessageBox.Show(dictionary[selectedItem].Count.ToString());
               // traner obj = new traner();
                //var x = await GetStudentData(dictionary[selectedItem][0].ToString());
                //var dynamicResult3 = JsonConvert.DeserializeObject<dynamic>(x);
                //MessageBox.Show(dynamicResult3.response.ToString());

                /*foreach (String pair in dictionary[selectedItem])
                {
                  var obj =   await GetStudentData(dictionary[selectedItem].ToString());

                }
                */

                // do something with the selected item
                //MessageBox.Show("Selected item: " + selectedItem);


            }
            catch (Exception e4)
            {
                MessageBox.Show("error ->>" + e4.Message);

            }
        }

        private void label15_Click(object sender, EventArgs e)
        {

        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.companyName = comboBox2.SelectedItem.ToString(); 
           //MessageBox.Show(comboBox2.SelectedItem.ToString()) ;
        }



        /////////////////////////////////////////////////////////////////
        ///
        public async Task initComboBoxItems()

        {
    
            if (!CheckForInternetConnection())
            {
                MessageBox.Show("Please check you connection and try again");
                return;
            }
            try
            {
                var x = await GetCompanyData();
                var dynamicResult2 = JsonConvert.DeserializeObject<dynamic>(x);
                Dictionary<string, string> dict = new Dictionary<string, string>();
                int count = dynamicResult2.response.count;
                var res = dynamicResult2.response.results;


                for (int i = 0; i < count; i++)
                {
                    String v1 = dynamicResult2.response.results[i]["_id"];
                    String v2 = dynamicResult2.response.results[i]["name"];

                    // Console.WriteLine(dynamicResult2.response.results[0]["name"]);
                    dict.Add(v1, v2);
                    comboBox2.Items.Add(v2);


                }

              //  MessageBox.Show("the company list initialized successfully");
                
         

  




            }
            catch (Exception ee)
            {
                Console.WriteLine(ee.Message);
                MessageBox.Show("error-->", ee.Message);
            }



        }

        public static async Task<string> GetCompanyData()
        {
            string content = null;

            String url = "https://www.tsti.ae/version-test/api/1.1/obj/companies";

            var client = new HttpClient();
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {

                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }

        private void label21_Click(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {

            try
            {
                this.Hide();

                // Close all open forms and exit the application
                Application.Exit();

                // Start a new instance of the application
                System.Diagnostics.Process.Start(Application.ExecutablePath);
            }
            catch(Exception exe)
            {
              

             MessageBox.Show("plz take pic and send it to tech team ->  ", exe.Message.ToString());
             Console.WriteLine(exe.Message);

            }

            /*
            if (rowCourser== 0 )
            {
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.served = "loading";
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.now =this.helperList[rowCourser].name;
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.next = this.helperList[rowCourser + 1].name;

            }
            if (rowCourser == this.helperList.Count)
            {
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.served =this.helperList[rowCourser - 1 ].name;
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.now = this.helperList[rowCourser].name;
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.next = "loading";

            }
            else
            {
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.served = this.helperList[rowCourser - 1].name;
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.now = this.helperList[rowCourser].name;
                ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.next = this.helperList[rowCourser + 1].name;

            }
            */



           // ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.next = "here louie frin main";
            //ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.Save();
            //string serializedTraneers = JsonConvert.SerializeObject(helperList);
            // ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.momo = serializedTraneers;
            // ZKTecoFingerPrintScanner_Implementation.Properties.Settings.Default.Save();
           // UserControl3 s = new UserControl3();
           // s.label5.Text = "Hello from the second window!";

        }
        ///////////////////////////////////////////////////////////////////////////////////////////////////











    }
}
