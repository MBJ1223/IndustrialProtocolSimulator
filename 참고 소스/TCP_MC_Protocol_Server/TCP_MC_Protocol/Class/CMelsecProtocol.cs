using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace TCP_MC_Protocol
{
    public class CMelsecProtocol
    {
        public CAsyncSockServer _MelsecServer;
        private List<byte[]> ReciveByteArr = new List<byte[]>();
        private List<int[]> ClientRequestData = new List<int[]>(); // 클라이언트에서 요구한 데이터

        public void ServerOpen(int OpenPort)
        {
            _MelsecServer = new CAsyncSockServer(OpenPort);
            InitDataWord();
        }

        public void ServerClose()
        {
            try
            {
                _MelsecServer.Dispose();
            }
            catch
            {

            }
            
        }

        private void InitDataWord()
        {
            MelsecDataWord dataWord;
            CGlobal.MelsecDataWordInfo.Clear();
            for (int i=0; i<=40000; i++)
            {
                dataWord = new MelsecDataWord();
                dataWord.WordName = "D" + i.ToString();
                int Temp = Convert.ToInt32((i.ToString()).GetLast(2));
                //if (Temp >= 10 && Temp <= 29)
                //{
                //    if(Temp < 20)
                //    {
                //        dataWord.WordValue = "AB";
                //    }
                //    else
                //    {
                //        dataWord.WordValue = "00";
                //    }
                //    dataWord.ASCII = "TRUE";
                //}
                //else if(Temp >= 60 && Temp <= 79)
                //{
                //    if (Temp < 70)
                //    {
                //        dataWord.WordValue = "AB";
                //    }
                //    else
                //    {
                //        dataWord.WordValue = "00";
                //    }
                //    dataWord.ASCII = "TRUE";
                //}
                //else
                //{
                    dataWord.WordValue = "0";
                    dataWord.ASCII = "FALSE";
                //}
                
                CGlobal.MelsecDataWordInfo.Add(dataWord);  
            }    
        }

        // 받은 데이터를 프로토콜과 비교 후에 클라이언트에 응답한다.
        public void RecivePacketCheck(byte[] RecivePacket)
        {
            try
            {
                ListBoxViewDataAddList(RecivePacket);
                ReciveByteArr.Add(RecivePacket);
                ProcessDataCompareProtocol();

            }
            catch(Exception ex)
            {
                System.Windows.MessageBox.Show("RecivePacketCheck Error");
                ReciveByteArr.RemoveAt(0);
            }      
        }


        // 패켓을 요청에 따라 처리한다.
        private bool ProcessDataCompareProtocol()
        {

            if(ReciveByteArr.Count > 0 )
            {
                int iCommand = BitConverter.ToInt16(ReciveByteArr[0], 11);
                List<int> TargetAddr = new List<int>();
                List<int> WriteValueData = new List<int>();
                switch (iCommand)
                {
                    case 1025: // 일괄 읽기 커맨드  0401
                        try
                        {
                            TargetAddr = TargetReadAddressAdd(ReciveByteArr[0], true);
                            ClientRequestData.Add(ClientRequestDataCheck(TargetAddr));
                            RecivePacketReadAck(ReciveByteArr[0], ClientRequestData[0], true);
                        }
                        catch
                        {
                            RecivePacketReadAck(ReciveByteArr[0], ClientRequestData[0], false);
                            System.Windows.MessageBox.Show("Total Read Packet Error");
                        }
                        ClientRequestData.Clear();
                        break;

                    case 1027: // 랜덤 읽기 커맨드  0403
                        try
                        {
                            TargetAddr = TargetReadAddressAdd(ReciveByteArr[0]);
                            ClientRequestData.Add(ClientRequestDataCheck(TargetAddr));
                            RecivePacketReadAck(ReciveByteArr[0], ClientRequestData[0], true);
                        }
                        catch
                        {
                            RecivePacketReadAck(ReciveByteArr[0], ClientRequestData[0], false);
                            System.Windows.MessageBox.Show("Random Read Packet Error");
                        }
                        ClientRequestData.Clear();
                        break;

                    case 5121: // 일괄 쓰기커맨드  1401
                        try
                        {
                            TargetAddr = TargetWriteAddressAdd(ReciveByteArr[0], true);
                            WriteValueData = TargetDataAdd(ReciveByteArr[0], true);
                            ClientRequestWriteDataApply(TargetAddr, WriteValueData);
                            RecivePacketWriteAck(ReciveByteArr[0], true);
                        }
                        catch
                        {
                            RecivePacketWriteAck(ReciveByteArr[0], false);
                            System.Windows.MessageBox.Show("Total Write Packet Error");
                        } 
                        break;

                    case 5122: // 랜덤 쓰기커맨드  1402
                        try
                        {
                            TargetAddr = TargetWriteAddressAdd(ReciveByteArr[0]);
                            WriteValueData = TargetDataAdd(ReciveByteArr[0]);
                            ClientRequestWriteDataApply(TargetAddr, WriteValueData);
                            RecivePacketWriteAck(ReciveByteArr[0], true);
                        }
                        catch
                        {
                            RecivePacketWriteAck(ReciveByteArr[0], false);
                            System.Windows.MessageBox.Show("Random Write Packet Error");
                        }
                        break;

                }

                ReciveByteArr.RemoveAt(0);
                return true;

            }

            return false;

        }

        //받은 패킷 데이터에서 클라이언트가 요청하는 실제 주소번지를 리스트에 넣어준다.
        private List<int> TargetReadAddressAdd(byte[] ReciveByte, bool bTotal = false)
        {

            List<int> DataAddr = new List<int>();
            int iRequestCount; 
            byte[] TempByte = new byte[4];

            if(bTotal) // 읽괄 읽기
            {
                iRequestCount = BitConverter.ToInt16(ReciveByte, 19);
                TempByte[0] = ReciveByte[15];
                TempByte[1] = ReciveByte[16];
                TempByte[2] = ReciveByte[17];
                TempByte[3] = 0x00;
                for (int i = 0; i < iRequestCount; i++)
                {
                    DataAddr.Add(BitConverter.ToInt32(TempByte, 0) + i);

                }
                
            }
            else // 랜덤 읽기
            {
                iRequestCount = BitConverter.ToInt16(ReciveByte, 15);
                for (int i = 0; i < iRequestCount; i++)
                {
                    TempByte[0] = ReciveByte[17 + i * 4];
                    TempByte[1] = ReciveByte[18 + i * 4];
                    TempByte[2] = ReciveByte[19 + i * 4];
                    TempByte[3] = 0x00;

                    DataAddr.Add(BitConverter.ToInt32(TempByte, 0));

                }
            }
            

            return DataAddr;
        }

        private List<int> TargetWriteAddressAdd(byte[] ReciveByte, bool bTotal = false)
        {
            List<int> DataAddr = new List<int>();
            int iRequestCount;
            byte[] TempByte = new byte[4];

            if (bTotal) // 읽괄 쓰기
            {
                iRequestCount = BitConverter.ToInt16(ReciveByte, 19);
                TempByte[0] = ReciveByte[15];
                TempByte[1] = ReciveByte[16];
                TempByte[2] = ReciveByte[17];
                TempByte[3] = 0x00;
                for (int i = 0; i < iRequestCount; i++)
                {
                    DataAddr.Add(BitConverter.ToInt32(TempByte, 0) + i);

                }
            }
            else // 랜덤 쓰기
            {
                iRequestCount = BitConverter.ToInt16(ReciveByte, 15);
                for (int i = 0; i < iRequestCount; i++)
                {
                    TempByte[0] = ReciveByte[17 + i * 6];
                    TempByte[1] = ReciveByte[18 + i * 6];
                    TempByte[2] = ReciveByte[19 + i * 6];
                    TempByte[3] = 0x00;

                    DataAddr.Add(BitConverter.ToInt32(TempByte, 0));
                }
            }
            

            return DataAddr;
        }

        //받은 패킷 데이터에서 클라이언트가 요청하는 실제 데이터(int)를 리스트에 넣어준다.
        private List<int> TargetDataAdd(byte[] ReciveByte, bool bTotal = false)
        {
            List<int> DataValue = new List<int>();
            int iRequestCount;
            byte[] TempByte = new byte[4];
            if (bTotal)
            {
                iRequestCount = BitConverter.ToInt16(ReciveByte, 19);

                for (int i = 0; i < iRequestCount; i++)
                {
                    TempByte[0] = ReciveByte[21 + i * 2];
                    TempByte[1] = ReciveByte[22 + i * 2];
                    TempByte[2] = 0x00;
                    TempByte[3] = 0x00;

                    DataValue.Add(BitConverter.ToInt32(TempByte, 0));
                }
            }
            else
            {
                iRequestCount = BitConverter.ToInt16(ReciveByte, 15);

                for (int i = 0; i < iRequestCount; i++)
                {
                    TempByte[0] = ReciveByte[21 + i * 6];
                    TempByte[1] = ReciveByte[22 + i * 6];
                    TempByte[2] = 0x00;
                    TempByte[3] = 0x00;

                    DataValue.Add(BitConverter.ToInt32(TempByte, 0));
                }
            }
           
            return DataValue;
        }

        //클라이언트에서 요청한 데이터를 확인 후 해당 값을 리턴해준다.
        private int[] ClientRequestDataCheck(List<int> TargetAddr)
        {
            int[] iTempArray = new int[TargetAddr.Count];

            for (int i = 0; i < TargetAddr.Count; i++)
            {
                bool bTempASCII = Convert.ToBoolean(CGlobal.MelsecDataWordInfo[TargetAddr[i]].ASCII);
                if (bTempASCII)
                {

                    iTempArray[i] = Write_ASCIIData(CGlobal.MelsecDataWordInfo[TargetAddr[i]].WordValue, CGlobal.MelsecDataWordInfo[TargetAddr[i]].WordValue.Length);
                }
                else
                {
                    iTempArray[i] = Convert.ToInt32(CGlobal.MelsecDataWordInfo[TargetAddr[i]].WordValue);
                }
            }

            return iTempArray;
        }
        // 받은 데이터 적용 값을 내부 데이터 번지(List)에 적용시킨다.
        private bool ClientRequestWriteDataApply(List<int> TargetAddr, List<int> ApplyData)
        {
            string ApplyAddress;
            string ApplayValue;
            for (int i = 0; i < TargetAddr.Count; i++)
            {
                ApplyAddress = CGlobal.MelsecDataWordInfo[TargetAddr[i]].WordName;
                bool bTempASCII = Convert.ToBoolean(CGlobal.MelsecDataWordInfo[TargetAddr[i]].ASCII);
                if (bTempASCII)
                {
                    ApplayValue = ASCIIByteToString(ApplyData[i]);
                }
                else
                {
                    ApplayValue = ApplyData[i].ToString();
                }
                
                CGlobal.MelsecDataWordInfo[TargetAddr[i]].WordValue = ApplayValue;
                CGlobal.RecivePacketProcessLog.Add(ApplyAddress + " : " + ApplayValue + " Each Value Apply Complete");

            } 
            return true;
        }



        #region 읽기, 쓰기
        // 읽기 응답
        private void RecivePacketReadAck(byte[] ReciveByteArr, int[] RequestData, bool bNoneError= true)
        {
            List<Byte> SendByte = new List<byte>();
            if(bNoneError) // 정상패킷
            {
                // 헤더 =========
                SendByte.Add(0xD0); //서브헤더
                SendByte.Add(0x00); //서브헤더
                SendByte.Add(ReciveByteArr[2]); //네트워크 번호
                SendByte.Add(ReciveByteArr[3]); //PLC 번호
                SendByte.Add(ReciveByteArr[4]); //I/O번호 요구상대 모듈 로우
                SendByte.Add(ReciveByteArr[5]); //I/O번호 요구상대 모듈 하이
                SendByte.Add(ReciveByteArr[6]);  // 요구상대 모듈 국번호
                // 응답 데이터 길이 확인 필요
                SendByte.Add((byte)(2 + (RequestData.Length * 2))); //응답 데이터 길이 
                SendByte.Add(0x00); //응답 데이터 길이 
                //==================================

                SendByte.Add(0x00); // 종료 코드 ( 정상 종료 ) 로우 
                SendByte.Add(0x00);  // 종료 코드 ( 정상 종료 ) 하이

                //데이터 부===========

                for (int i = 0; i < RequestData.Length; i++)
                {
                    byte[] TempbyteArr = BitConverter.GetBytes((short)RequestData[i]);

                    if (BitConverter.IsLittleEndian)
                    {
                        SendByte.Add(TempbyteArr[0]);
                        SendByte.Add(TempbyteArr[1]);
                    }

                }

            }
            else // 에러 패킷 응답
            {
                SendByte.Add(0xD0); //서브헤더
                SendByte.Add(0x00); //서브헤더
                SendByte.Add(ReciveByteArr[2]); //네트워크 번호
                SendByte.Add(ReciveByteArr[3]); //PLC 번호
                SendByte.Add(ReciveByteArr[4]); //I/O번호 요구상대 모듈 로우
                SendByte.Add(ReciveByteArr[5]); //I/O번호 요구상대 모듈 하이
                SendByte.Add(ReciveByteArr[6]);  // 요구상대 모듈 국번호
                // 응답 데이터 길이 확인 필요
                SendByte.Add(0x0B); //응답 데이터 길이 
                SendByte.Add(0x00); //응답 데이터 길이 
                //==================================
                // 에러 정보부
                SendByte.Add(0xA2); // 종료 코드 ( 에러 종료 ) 로우 
                SendByte.Add(0x00);  // 종료 코드 ( 에러 종료 ) 하이 
                SendByte.Add(ReciveByteArr[2]); //네트워크 번호
                SendByte.Add(ReciveByteArr[3]); //PLC 번호
                SendByte.Add(ReciveByteArr[4]); //I/O번호 요구상대 모듈 로우
                SendByte.Add(ReciveByteArr[5]); //I/O번호 요구상대 모듈 하이
                SendByte.Add(ReciveByteArr[6]);  // 요구상대 모듈 국번호
                SendByte.Add(ReciveByteArr[11]); // 커맨드  로우
                SendByte.Add(ReciveByteArr[12]); // 커맨드  하이
                SendByte.Add(ReciveByteArr[13]); // 서브 커맨드 로우
                SendByte.Add(ReciveByteArr[14]); // 서브 커맨드 하이

            }


            try
            {
                ListBoxViewDataAddList(SendByte.ToArray(), true);
                _MelsecServer.Send(SendByte.ToArray());
                CGlobal.RecivePacketProcessLog.Add(RequestData.Length + " Data Value ReadPacket Send");
            }
            catch
            {
                CGlobal.RecivePacketProcessLog.Add("Read Ack Packet Send Fail");
            }
        }
        // 쓰기 응답
        private void RecivePacketWriteAck(byte[] ReciveByteArr, bool bNoneError = true)
        {
            List<Byte> SendByte = new List<byte>();
            if (bNoneError) // 정상패킷
            {// 헤더 =========
                SendByte.Add(0xD0); //서브헤더
                SendByte.Add(0x00); //서브헤더
                SendByte.Add(ReciveByteArr[2]); //네트워크 번호
                SendByte.Add(ReciveByteArr[3]); //PLC 번호
                SendByte.Add(ReciveByteArr[4]); //I/O번호 요구상대 모듈 로우
                SendByte.Add(ReciveByteArr[5]); //I/O번호 요구상대 모듈 하이
                SendByte.Add(ReciveByteArr[6]);  // 요구상대 모듈 국번호
                // 응답 데이터 길이
                SendByte.Add(0x02); //응답 데이터 길이 쓰기 요청 응답시에는 뒤에 종료코드만 붙기 때문에 무조건 2
                SendByte.Add(0x00); //응답 데이터 길이 
                //==================================
                SendByte.Add(0x00); // 종료 코드 ( 정상 종료 ) 로우 
                SendByte.Add(0x00);  // 종료 코드 ( 정상 종료 ) 하이

            }
            else // 에러 패킷 응답
            {
                SendByte.Add(0xD0); //서브헤더
                SendByte.Add(0x00); //서브헤더
                SendByte.Add(ReciveByteArr[2]); //네트워크 번호
                SendByte.Add(ReciveByteArr[3]); //PLC 번호
                SendByte.Add(ReciveByteArr[4]); //I/O번호 요구상대 모듈 로우
                SendByte.Add(ReciveByteArr[5]); //I/O번호 요구상대 모듈 하이
                SendByte.Add(ReciveByteArr[6]);  // 요구상대 모듈 국번호
                // 응답 데이터 길이 확인 필요
                SendByte.Add(0x0B); //응답 데이터 길이 
                SendByte.Add(0x00); //응답 데이터 길이 
                //==================================
                // 에러 정보부
                SendByte.Add(0xA2); // 종료 코드 ( 에러 종료 ) 로우 
                SendByte.Add(0x00);  // 종료 코드 ( 에러 종료 ) 하이 
                SendByte.Add(ReciveByteArr[2]); //네트워크 번호
                SendByte.Add(ReciveByteArr[3]); //PLC 번호
                SendByte.Add(ReciveByteArr[4]); //I/O번호 요구상대 모듈 로우
                SendByte.Add(ReciveByteArr[5]); //I/O번호 요구상대 모듈 하이
                SendByte.Add(ReciveByteArr[6]);  // 요구상대 모듈 국번호
                SendByte.Add(ReciveByteArr[11]); // 커맨드  로우
                SendByte.Add(ReciveByteArr[12]); // 커맨드  하이
                SendByte.Add(ReciveByteArr[13]); // 서브 커맨드 로우
                SendByte.Add(ReciveByteArr[14]); // 서브 커맨드 하이

            }


            try
            {
                ListBoxViewDataAddList(SendByte.ToArray(), true);
                _MelsecServer.Send(SendByte.ToArray());
            }
            catch
            {
                CGlobal.RecivePacketProcessLog.Add("Write Ack Packet Send Fail");
            }
        }
        #endregion

       
        private void ListBoxViewDataAddList(byte[] SendPacketByte, bool bSend = false)
        {
            if (bSend)
            {
                string sTemp = "Tx ";
                for (int i = 0; i < SendPacketByte.Length / 2; i++)
                {
                    //string OriginHex = BitConverter.ToString(SendPacketByte, i * 2, 2);
                    //OriginHex = OriginHex.Replace("-", " ");
                    //sTemp = sTemp + " " + OriginHex;
                    sTemp = sTemp + " " + SendPacketByte[i].ToString();
                }
                CGlobal.RecivePacketOriginLog.Add(sTemp);
            }
            else
            {
                string sTemp = "Rx ";
                for (int i = 0; i < SendPacketByte.Length; i++)
                {
                    //string OriginHex = BitConverter.ToString(SendPacketByte, i * 2, 2);
                    //OriginHex = OriginHex.Replace("-", " ");
                    //sTemp = sTemp + " " + OriginHex;
                    sTemp = sTemp + " " + SendPacketByte[i].ToString();
                }
                CGlobal.RecivePacketOriginLog.Add(sTemp);
            }
        }

        #region 참조
        private byte[] IntToByteArray(int iValue)
        {
            byte[] intBytes = BitConverter.GetBytes(iValue);
            Array.Reverse(intBytes);
            return intBytes;
        }

        public static byte[] StrToByteArray(string str)
        {
            Dictionary<string, byte> hexindex = new Dictionary<string, byte>();
            for (int i = 0; i <= 255; i++)
                hexindex.Add(i.ToString("X2"), (byte)i);

            List<byte> hexres = new List<byte>();
            for (int i = 0; i < str.Length; i += 2)
                hexres.Add(hexindex[str.Substring(i, 2)]);

            return hexres.ToArray();
        }

        private int Write_ASCIIData(string p_strData_toPLC, int p_ntMaxTextLength)
        {
            int l_ntTempWriteValue = 0;


            //Write data from PLC
            string l_strTempWriteValue = p_strData_toPLC;

            double l_dlTempLength = (p_ntMaxTextLength + 1) / 2;
            double l_dlTempHalfLength = Math.Truncate(l_dlTempLength);

            for (int i = 0; i < l_dlTempHalfLength; i++)
            {
                //Text to ASCII
                char l_crHighByte, l_crLowByte;
                int l_ntHighByte, l_ntLowByte;

                if ((i * 2) < l_strTempWriteValue.Length)
                {
                    l_crLowByte = Convert.ToChar(l_strTempWriteValue.Substring(i * 2, 1));
                    l_ntLowByte = Convert.ToInt32(l_crLowByte);
                }
                else
                {
                    l_ntLowByte = 0;
                }

                if ((i * 2) + 1 < l_strTempWriteValue.Length)
                {
                    l_crHighByte = Convert.ToChar(l_strTempWriteValue.Substring((i * 2) + 1, 1));
                    l_ntHighByte = Convert.ToInt32(l_crHighByte);
                }
                else
                {
                    l_ntHighByte = 0;
                }

                //Binarization & Attatch two Text
                string l_strBinLowByte = Convert.ToString(l_ntLowByte, 2).PadLeft(8, '0');
                string l_strBinHighByte = Convert.ToString(l_ntHighByte, 2).PadLeft(8, '0');
                string l_strAttatchedBin = l_strBinHighByte + l_strBinLowByte;

                l_ntTempWriteValue = Convert.ToInt32(l_strAttatchedBin, 2);

                //Write data to PLC and Complete Sig.
                //l_blTempResult &= WriteOneWord(l_strTempMemKind + (l_ntTempAddr + i).ToString(), l_ntTempWriteValue);
            }

            return l_ntTempWriteValue;
        }

        private string ASCIIByteToString(int ReadData)
        {
            int value = Convert.ToInt16(ReadData);
            byte lowByte = (byte)(value & 0xff);
            byte highByte = (byte)((value >> 8) & 0xff);
            char ReadFirstChar = Convert.ToChar(lowByte);
            char ReadSecondChar = Convert.ToChar(highByte);
            string ReadString = Convert.ToString(ReadFirstChar) + Convert.ToString(ReadSecondChar);
            return ReadString;
        }

        #endregion
    }

    public class CAsyncSockServer
    {
        public Socket m_ServerSocket;
        public List<Socket> m_List_ConnetedSocket;
        IPAddress thisAddress;
        public byte[] szData;
        public int mn_SocketServerStatus = -1;  //Disconnected

        public Queue RxQueue = new Queue();

        public string mstr_ReceivedData;
        public bool mb_DataReceived = false;

        public CAsyncSockServer(int pn_PortNO)
        {
            m_List_ConnetedSocket = new List<Socket>();

            m_ServerSocket = new Socket(
                                AddressFamily.InterNetwork,
                                SocketType.Stream,
                                ProtocolType.Tcp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, pn_PortNO);
            m_ServerSocket.Bind(ipep);
            m_ServerSocket.Listen(20);
            //m_ServerSocket.ReceiveTimeout = 5000;
            //m_ServerSocket.SendTimeout = 5000;
            mn_SocketServerStatus = 1;  //Listening

            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += new EventHandler<SocketAsyncEventArgs>(Accept_Completed);
            m_ServerSocket.AcceptAsync(args);
        }

        public int Get_SocketStatus()
        {
            return mn_SocketServerStatus;
        }

        public void Listen()
        {
            try
            {
                m_ServerSocket.Listen(20);
                mn_SocketServerStatus = 1;  //Listening

                m_List_ConnetedSocket.Clear();
            }
            catch
            {
                mn_SocketServerStatus = -1;  //Disconnected
            }
        }

        private void Accept_Completed(object sender, SocketAsyncEventArgs e)
        {
            Socket ClientSocket = e.AcceptSocket;
            m_List_ConnetedSocket.Add(ClientSocket);

            if (m_List_ConnetedSocket != null)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                szData = new byte[4096];
                args.SetBuffer(szData, 0, 4096);
                args.UserToken = m_List_ConnetedSocket;
                args.Completed
                    += new EventHandler<SocketAsyncEventArgs>(Receive_Completed);

                ClientSocket.ReceiveAsync(args);
            }
            e.AcceptSocket = null;
            m_ServerSocket.AcceptAsync(e);

            mn_SocketServerStatus = 2;  //Connected
        }

        private void Receive_Completed(object sender, SocketAsyncEventArgs e)
        {
            Socket ClientSocket = (Socket)sender;
            //string lstr_RecvData;
            try
            {
                if (ClientSocket.Connected && e.BytesTransferred > 0)
                {
                    byte[] szData = e.Buffer;

                    ClientSocket.ReceiveAsync(e);

                    int iDataByteLength = BitConverter.ToInt16(szData, 7) + 9;

                    if (iDataByteLength == e.BytesTransferred)
                    {
                        byte[] readPackets = new byte[iDataByteLength];
                        for (int i = 0; i < iDataByteLength; i++) readPackets[i] = szData[i];

                        CGlobal.MelsecProtocol.RecivePacketCheck(readPackets);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Packet Length Error!");
                    }
                }
                else
                {
                    ClientSocket.Close();
                    try { ClientSocket.Dispose(); } catch { }
                    m_List_ConnetedSocket.RemoveAt(Get_TargetClientSocket(ClientSocket));
                }
            }
            catch (Exception ex)
            {
                ClientSocket.Close();
                try { ClientSocket.Dispose(); } catch { }
                m_List_ConnetedSocket.RemoveAt(Get_TargetClientSocket(ClientSocket));
            }
        }

        private int Get_TargetClientSocket(Socket socket)
        {
            int i = 0;
            for (i=0; i< m_List_ConnetedSocket.Count; i++)
            {
                if(socket == m_List_ConnetedSocket[i])
                {
                    return i;
                }
            }

            return i;
        }

        public bool Send(byte[] byteData)
        {
            if (m_List_ConnetedSocket == null)
            {
                return false;
            }

            try
            {
                if (m_List_ConnetedSocket.Count > 0)
                {
                    m_List_ConnetedSocket[0].Send(byteData);
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            foreach (Socket pBuffer in m_List_ConnetedSocket)
            {
                if (pBuffer.Connected)
                    pBuffer.Disconnect(false);
                pBuffer.Dispose();
            }
            m_ServerSocket.Shutdown(SocketShutdown.Both);
            m_ServerSocket.Close();
            m_ServerSocket.Dispose();
        }

        //protected void Dispose()
        //{
        //    foreach (Socket pBuffer in m_List_ConnetedSocket)
        //    {
        //        if (pBuffer.Connected)
        //            pBuffer.Disconnect(false);
        //        pBuffer.Dispose();
        //    }
        //    m_ServerSocket.Dispose();
        //}
    }

    //public class CSockServer
    //{
    //    public Socket m_ServerSocket;
    //    public List<Socket> m_List_ConnetedSocket;
    //    public byte[] szData;
    //    public int mn_SocketServerStatus = -1;  //Disconnected

    //    public Queue RxQueue = new Queue();

    //    public string mstr_ReceivedData;
    //    public bool mb_DataReceived = false;

    //    public CSockServer()
    //    {
    //        m_List_ConnetedSocket = new List<Socket>();

    //        m_ServerSocket = new Socket(
    //                            AddressFamily.InterNetwork,
    //                            SocketType.Stream,
    //                            ProtocolType.Tcp);
    //        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 3000);
    //        m_ServerSocket.Bind(ipep);
    //        m_ServerSocket.Listen(20);
    //        mn_SocketServerStatus = 1;  //Listening

    //        SocketAsyncEventArgs args = new SocketAsyncEventArgs();
    //        args.Completed
    //            += new EventHandler<SocketAsyncEventArgs>(Accept_Completed);
    //        m_ServerSocket.AcceptAsync(args);
    //    }

    //    public int Get_SocketStatus()
    //    {
    //        return mn_SocketServerStatus;
    //    }

    //    public CSockServer(int pn_PortNO)
    //    {
    //        m_List_ConnetedSocket = new List<Socket>();

    //        m_ServerSocket = new Socket(
    //                            AddressFamily.InterNetwork,
    //                            SocketType.Stream,
    //                            ProtocolType.Tcp);
    //        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, pn_PortNO);
    //        m_ServerSocket.Bind(ipep);
    //        m_ServerSocket.Listen(20);
    //        //m_ServerSocket.ReceiveTimeout = 5000;
    //        //m_ServerSocket.SendTimeout = 5000;
    //        mn_SocketServerStatus = 1;  //Listening

    //        SocketAsyncEventArgs args = new SocketAsyncEventArgs();
    //        args.Completed += new EventHandler<SocketAsyncEventArgs>(Accept_Completed);
    //        m_ServerSocket.AcceptAsync(args);
    //    }

    //    public void Listen()
    //    {
    //        try
    //        {
    //            m_ServerSocket.Listen(20);
    //            mn_SocketServerStatus = 1;  //Listening

    //            m_List_ConnetedSocket.Clear();
    //        }
    //        catch
    //        {
    //            mn_SocketServerStatus = -1;  //Disconnected
    //        }
    //    }

    //    public bool Send(byte[] byteData)
    //    {
    //        if (m_List_ConnetedSocket == null)
    //        {
    //            return false;
    //        }

    //        try
    //        {
    //            if (m_List_ConnetedSocket.Count > 0)
    //            {
    //                m_List_ConnetedSocket[0].Send(byteData);
    //            }
    //            else
    //            {
    //                return false;
    //            }
    //        }
    //        catch
    //        {
    //            return false;
    //        }

    //        return true;
    //    }

    //    private void Accept_Completed(object sender, SocketAsyncEventArgs e)
    //    {
    //        Socket ClientSocket = e.AcceptSocket;
    //        m_List_ConnetedSocket.Add(ClientSocket);

    //        if (m_List_ConnetedSocket != null)
    //        {
    //            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
    //            szData = new byte[4096];
    //            args.SetBuffer(szData, 0, 4096);
    //            args.UserToken = m_List_ConnetedSocket;
    //            args.Completed
    //                += new EventHandler<SocketAsyncEventArgs>(Receive_Completed);

    //            ClientSocket.ReceiveAsync(args);
    //        }
    //        e.AcceptSocket = null;
    //        m_ServerSocket.AcceptAsync(e);

    //        mn_SocketServerStatus = 2;  //Connected
    //    }

    //    private void Receive_Completed(object sender, SocketAsyncEventArgs e)
    //    {
    //        Socket ClientSocket = (Socket)sender;
    //        //string lstr_RecvData;
    //        try
    //        {
    //            if (ClientSocket.Connected && e.BytesTransferred > 0)
    //            {
    //                byte[] szData = e.Buffer;

    //                ClientSocket.ReceiveAsync(e);

    //                int iDataByteLength = BitConverter.ToInt16(szData, 7) + 9;

    //                if(iDataByteLength == e.BytesTransferred)
    //                {
    //                    byte[] readPackets = new byte[iDataByteLength];
    //                    for (int i = 0; i < iDataByteLength; i++) readPackets[i] = szData[i];

    //                    CGlobal.MelsecProtocol.RecivePacketCheck(readPackets);
    //                }
    //                else
    //                {
    //                    System.Windows.MessageBox.Show("Packet Length Error!");
    //                }
                    

    //            } 
    //        }
    //        catch(Exception ex)
    //        {
    //            //mb_wsk_DataArrival = false;
    //        }
    //    }
    //    protected void Dispose()
    //    {
    //        foreach (Socket pBuffer in m_List_ConnetedSocket)
    //        {
    //            if (pBuffer.Connected)
    //                pBuffer.Disconnect(false);
    //            pBuffer.Dispose();
    //        }
    //        m_ServerSocket.Dispose();
    //    }
    //}
    public static class StringExtension
    {
        public static string GetLast(this string source, int tail_length)
        {
            if (tail_length >= source.Length)
                return source;
            return source.Substring(source.Length - tail_length);
        }
    }
}
