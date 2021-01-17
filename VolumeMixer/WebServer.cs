using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// FROM https://www.c-sharpcorner.com/article/creating-your-own-web-server-using-C-Sharp/

public class WebServer {
    private TcpListener listener;
    private int port = 5050;

    public WebServer() {
        try {
            //start listing on the given port  
#pragma warning disable CS0618 // Type or member is obsolete
            listener = new TcpListener(port);
#pragma warning restore CS0618 // Type or member is obsolete
            listener.Start();
            Log("Web Server Running... Press ^C to Stop...");
            //start the thread which calls the method 'StartListen'  
            Thread th = new Thread(new ThreadStart(StartListen));
            th.Start();
        } catch (Exception e) {
            Log("An Exception Occurred while Listening :" + e.ToString());
        }
    }

    private void Log(string msg, DateTime? date = null) {
        if (date == null) {
            date = DateTime.Now;
        }
        Console.WriteLine("[" + date + "]: " + msg);
    }

    public void SendToBrowser(String sData, ref Socket socket) {
        SendToBrowser(Encoding.ASCII.GetBytes(sData), ref socket);
    }

    public void SendToBrowser(Byte[] bSendData, ref Socket socket) {
        int numBytes = 0;
        try {
            if (socket.Connected) {
                if ((numBytes = socket.Send(bSendData, bSendData.Length, 0)) == -1)
                    Log("Socket Error cannot Send Packet");
                else {
                    Log(String.Format("No. of bytes sent: {0}", numBytes));
                }
            } else Log("Connection Dropped....");
        } catch (Exception e) {
            Log(String.Format("Error Occurred : {0} ", e));
        }
    }

    public void SendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, ref Socket socket) {
        String sBuffer = "";
        // if Mime type is not provided set default to text/html  
        if (sMIMEHeader.Length == 0) {
            sMIMEHeader = "text/html";// Default Mime Type is text/html  
        }
        sBuffer = sBuffer + sHttpVersion + sStatusCode + "\r\n";
        sBuffer = sBuffer + "Server: cx1193719-b\r\n";
        sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
        sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
        sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n\r\n";
        Byte[] bSendData = Encoding.ASCII.GetBytes(sBuffer);
        SendToBrowser(bSendData, ref socket);
        Log("Total Bytes : " + iTotBytes.ToString());
    }

    public void StartListen() {
        int iStartPos = 0;
        String sRequest;
        while (true) {
            //Accept a new connection  
            Socket socket = listener.AcceptSocket();
            if (socket.Connected) {
                Log(String.Format("\nClient Connected!!\n==================\nClient IP {0}\n", socket.RemoteEndPoint));
                //make a byte array and receive data from the client   
                Byte[] bReceive = new Byte[1024];
                int i = socket.Receive(bReceive, bReceive.Length, 0);
                //Convert Byte to String  
                string sBuffer = Encoding.ASCII.GetString(bReceive);
                //At present we will only deal with GET type  
                if (sBuffer.Substring(0, 3) != "GET") {
                    Log("Only Get Method is supported..");
                    socket.Close();
                }
                // Look for HTTP request  
                iStartPos = sBuffer.IndexOf("HTTP", 1);
                // Get the HTTP text and version e.g. it will return "HTTP/1.1"  
                string sHttpVersion = sBuffer.Substring(iStartPos, 8);
                // Extract the Requested Type and Requested file/directory  
                sRequest = sBuffer.Substring(0, iStartPos - 1);
                //Replace backslash with Forward Slash, if Any  
                sRequest.Replace("\\", "/");
                string output = "No options provided";
                if (sRequest.Contains("?")) {
                    string[] parameters = sRequest.Substring(sRequest.IndexOf("?") + 1).Split('&');
                    IDictionary<string, string> opts = new Dictionary<string, string>();
                    foreach (var parameter in parameters) {
                        string[] opt = parameter.Split('=');
                        Log(parameter);
                        if (opt.Length == 2) {
                            opts.Add(opt[0], opt[1]);
                        } else {
                            opts.Add(parameter, "");
                        }
                    }

                    string pName = "";
                    if (opts.ContainsKey("idType") && opts["idType"] == "name") {
                        pName = opts["name"];
                    } else if (opts.ContainsKey("idType") && opts["idType"] == "pid") {
                        int pid = Int32.Parse(opts["pid"]);
                        foreach (var process in Process.GetProcesses()) {
                            if (process.Id == pid) {
                                pName = process.ProcessName;
                            }
                        }
                    } else {
                        pName = VolumeMixerInterFace.GetForegroundProcessName();
                    }

                    if (opts["operation"] == "setVolume") {
                        if (float.TryParse(opts["param"], out float vol)) {
                            output = "Set " + pName + " to " + vol + "%.";
                            VolumeMixerInterFace.SetApplicationVolume(pName, vol);
                        } else {
                            output = "Not a valid volume.";
                        }
                    } else if (opts["operation"] == "setMute") {
                        bool mute = bool.Parse(opts["param"]);
                        VolumeMixerInterFace.SetApplicationMute(pName, mute);
                        output = "Set " + pName + " to be " + (mute ? "" : "un") + "muted.";
                    } else if (opts["operation"] == "toggleMute") {
                        VolumeMixerInterFace.ToggleApplicationMute(pName);
                        output = "Toggled " + pName + "'s muted-ness";
                    } else if (opts["operation"] == "incrementVolume") {
                        if (float.TryParse(opts["param"], out float volInc)) {
                            output = (volInc > 0 ? "+" : "") + volInc + "% to " + pName + ".";
                            VolumeMixerInterFace.IncrementApplicationVolume(pName, volInc);
                        } else {
                            output = "Not a valid increment value.";
                        }
                    }
                }

                SendHeader(sHttpVersion, "text/plain", output.Length, " 200 OK", ref socket);
                SendToBrowser(output, ref socket);
                socket.Close();
                continue;
            }
        }
    }
}
