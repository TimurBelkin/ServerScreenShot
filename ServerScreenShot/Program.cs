using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Net.Sockets;
using System.IO;
using NLog;

namespace ServerScreenShot
{
    class Program
    {
        static void Main(string[] args)
        {
            Snoop snoop = new Snoop();
            snoop.Start();
        }
    }

    class Snoop
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private int COPYPERIOD = Int32.Parse(ConfigurationManager.AppSettings["CopyPeriod"]);
        private string IMAGEDIRECTORY = ConfigurationManager.AppSettings["ImageDirectory"];
        public void Start()
        {
            ServerStart server = new ServerStart();
            var t = Task.Run(() => server.Start());
            while (true)
            {
                Rectangle bounds = Screen.GetBounds(Point.Empty);
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    string fileName = String.Format("ScreenShot{0}.jpg", DateTime.Now.ToString("dd_MM_yy_HH_mm_ss_tt"));
                    string imgFileName = IMAGEDIRECTORY + @"\" + fileName;
                    bitmap.Save(imgFileName, ImageFormat.Jpeg);
                    logger.Info("File {0} saved", imgFileName);
                }
                System.Threading.Thread.Sleep(COPYPERIOD);
            }
        }


        class ServerStart
        {
            public  string IMAGEDIRECTORY = ConfigurationManager.AppSettings["ImageDirectory"];
            private const string IP = "127.0.0.1";
            public void Start()
            {
                logger.Info("Start");
                TcpListener server = null;
                try
                {
                    Int32 port = 13000;
                    IPAddress localAddr = IPAddress.Parse(IP);
                    server = new TcpListener(localAddr, port);

                    server.Start();

                    Byte[] bytes = new Byte[256];
                    String data = null;

                    bool serverTrigger = true;
                    while (serverTrigger)
                    {
                        try
                        {
                            Console.Write("Waiting for a connection... ");

                            TcpClient client = server.AcceptTcpClient();
                            Console.WriteLine("Connected!");
                            NetworkStream stream = client.GetStream();
                            string inputString = receiveMessage(stream);
                            Console.WriteLine("Received: {0}", inputString);
                            sendMessage("true", stream);
                            Console.WriteLine("Sent: {0}", "true");
                            bool flag = true;
                            logger.Info("Server has been created IP {0} port {1}", "127.0.0.1", port);
                            while (flag)
                            {
                                string command = receiveMessage(stream);
                                switch (command)
                                {
                                    case "all":
                                        Console.WriteLine("the method all isn't ready yet");
                                        break;
                                    case "list":
                                        sendFileList(IMAGEDIRECTORY, stream);
                                        break;
                                    case "single":
                                        sendImage(stream);
                                        break;
                                    case "check":
                                        checkFile(stream);
                                        break;
                                    case "stop":
                                    case "exit":
                                        flag = false;
                                        break;

                                    default:
                                        Console.WriteLine("the inputted command isn't correct");
                                        flag = false;
                                        break;
                                }
                            }

                            stream.Close();
                            client.Close();
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Ecxeption {0}", ex.Message);
                            Console.WriteLine("Exception {0}", ex.Message);
                        }
                    }
                }
                catch (SocketException e)
                {
                    logger.Error("Ecxeption {0}", e.Message);
                    Console.WriteLine("SocketException: {0}", e);
                }
                catch (Exception ex)
                {
                    logger.Error("Ecxeption {0}", ex.Message);
                    Console.WriteLine("Exception {0}", ex);
                }
                finally
                {
                    server.Stop();
                }


                Console.WriteLine("\nHit enter to continue...");
                Console.Read();
            }

            void checkFile(NetworkStream stream)
            {
                logger.Info("checkFile");
                string imgFileName = receiveMessage(stream);
                if (ifFileExists(imgFileName))
                {
                    logger.Info("file exists");
                    sendMessage("true", stream);
                }
                else
                {
                    logger.Info("file doesn't exist");
                    sendMessage("false", stream);
                }
            }
            bool ifFileExists(string imgFileName)
            {
                logger.Info("ifFileExists");
                Console.WriteLine("file name is {0}", imgFileName);
                logger.Info("file name is {0}", imgFileName);
                imgFileName = IMAGEDIRECTORY + @"\" + imgFileName;
                if (File.Exists(imgFileName))
                {
                    return true;
                }
                return false;
            }


            void sendImage(NetworkStream stream)
            {
                logger.Info("sendImage");
                lock (this)
                {
                    Console.WriteLine("Sending Image");
                    string imgFileName = receiveMessage(stream);
                    Stream imageFileStream;
                    if (ifFileExists(imgFileName))
                    {
                        imgFileName = IMAGEDIRECTORY + @"\" + imgFileName;
                        using (imageFileStream = File.OpenRead(imgFileName))
                        {
                            Console.WriteLine("file {0} found in the directory", imgFileName);
                            logger.Info("file has been found in directory");
                            sendStream(imageFileStream, stream);
                        }
                    }
                    else
                    {
                        Console.WriteLine("file wasn't found");
                        logger.Info("file hasn't been found in directory");
                    }
                }

                Console.WriteLine("Image was sent");
            }

            string receiveMessage(NetworkStream stream)
            {
                logger.Info("recieveMessage");
                Byte[] bytes = new Byte[4];
                int massageLength;
                string dataRecived = null;
                while ((massageLength = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    int dataLength = BitConverter.ToInt32(bytes, 0);
                    logger.Info("received data length {0}", dataLength.ToString());
                    Byte[] dataBytes = new Byte[dataLength];
                    while ((dataLength = stream.Read(dataBytes, 0, dataBytes.Length)) != 0)
                    {
                        logger.Info("dataLength (has to be equal data length) {0}", dataLength.ToString());
                        dataRecived = System.Text.Encoding.ASCII.GetString(dataBytes, 0, dataLength);
                        break;
                    }
                    break;
                }
                logger.Info("recieved data is {0}", dataRecived);
                return dataRecived;
            }

            void sendStream(Stream streamToSend, NetworkStream stream)
            {
                logger.Info("sendStream");
                Byte[] dataToSend = new byte[streamToSend.Length];
                streamToSend.Read(dataToSend, 0, (int)streamToSend.Length);
                logger.Info("size of sending data {0}", dataToSend.Length.ToString());
                sendBytes(dataToSend, stream);
            }

            void sendMessage(string message, NetworkStream stream)
            {
                logger.Info("sendMessage");
                logger.Info("message is {0}", message);
                Byte[] dataToSend = System.Text.Encoding.ASCII.GetBytes(message);
                sendBytes(dataToSend, stream);
            }

            void sendFileList(string path, NetworkStream stream)
            {
                logger.Info("sendFileLIst");
                Console.WriteLine("send file list");
                string[] fileEntries = Directory.GetFiles(path);
                byte[] dataAsBytes = fileEntries.SelectMany(s => System.Text.Encoding.ASCII.GetBytes(s + '|')).ToArray();
                dataAsBytes = dataAsBytes.Take(dataAsBytes.Count() - 1).ToArray();
                sendBytes(dataAsBytes, stream);
            }

            void sendBytes(Byte[] dataToSend, NetworkStream stream)
            {
                Byte[] dataLength = BitConverter.GetBytes((int)dataToSend.Length);
                Console.WriteLine("length of data to send {0}", (int)dataToSend.Length);
                Console.WriteLine("length of data to send after converting {0}", BitConverter.ToInt32(dataLength, 0));
                logger.Info("data length is {0}", dataLength.Length.ToString());
                stream.Write(dataLength, 0, 4);
                logger.Info("dataLength was sent");
                stream.Write(dataToSend, 0, dataToSend.Length);
                logger.Info("data was sent");
            }

        }
    }
}
