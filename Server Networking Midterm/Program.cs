﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace Server_Networking_Midterm
{
    class Program
    {
        private static byte[] buffer = new byte[1024];

        //TCP Stuff
        private static Socket TCPServer;
        private static Socket client1TCP = null;
        private static Socket client2TCP = null;

        //UDP Stuff
        private static Socket UDPServer;
        private static IPEndPoint client1UDP = null;
        private static IPEndPoint client2UDP = null;
        private static float[] clientPosVeloData = new float[6];

        //Client Names and Scores
        private static float client1Score = 0;
        private static float client2Score = 0;
        private static string client1Name = "defaultclient1";
        private static string client2Name = "defaultclient2";

        //Filepath Stuff
        private static string fileName = "Leaderboard.txt";
        private static string filePath = Path.Combine(Environment.CurrentDirectory, fileName);

        //Leaderboard stuff
        private static string leaderboardString;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Server... ");

            //TCP Setup 
            TCPServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            TCPServer.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888));
            TCPServer.Listen(2);
            Console.WriteLine("TCP Server listening on port 8888...");


            //UDP Setup
            UDPServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UDPServer.Bind(new IPEndPoint(IPAddress.Any, 8889));
            Console.WriteLine("UDP Server listening on port 8889...");

            if (File.Exists(filePath))
            {
                string[] allLines = File.ReadAllLines(filePath);
                leaderboardString = $"LeaderboardVals: {allLines[0]}\n{allLines[1]}\n{allLines[2]}\n{allLines[3]}\n{allLines[4]}";
            }

            TCPServer.BeginAccept(new AsyncCallback(AcceptTCPCallback), null);

            Thread UDPThread = new Thread(ReceiveUDPData);
            UDPThread.Start();
            StartCoinSpawning();
            Console.ReadLine();
        }

        //Chat / TCP: 
        private static void AcceptTCPCallback(IAsyncResult result)
        {
            Socket socket = TCPServer.EndAccept(result);

            //Assigns TCP client 1 and 2 or rejects additional client if more than 2
            if (client1TCP == null)
            {
                client1TCP = socket;
                Console.WriteLine("Client 1 TCP connected: " + client1TCP.RemoteEndPoint);
                byte[] leaderboardMSG = Encoding.ASCII.GetBytes(leaderboardString);
                client1TCP.BeginSend(leaderboardMSG, 0, leaderboardMSG.Length, 0, new AsyncCallback(SendCallback), client1TCP);
            }
            else if (client2TCP == null)
            {
                client2TCP = socket;
                Console.WriteLine("Client 2 TCP connected: " + client2TCP.RemoteEndPoint);
                byte[] leaderboardMSG = Encoding.ASCII.GetBytes(leaderboardString);
                client2TCP.BeginSend(leaderboardMSG, 0, leaderboardMSG.Length, 0, new AsyncCallback(SendCallback), client2TCP);
            }
            else
            {
                Console.WriteLine("Server is full. Rejecting additional client.");
                socket.Close();
                return;
            }

            socket.BeginReceive(buffer, 0, buffer.Length, 0, new AsyncCallback(ReceiveTCPCallback), socket);
            TCPServer.BeginAccept(new AsyncCallback(AcceptTCPCallback), null);
        }

        private static void ReceiveTCPCallback(IAsyncResult result)
        {
            Socket socket = (Socket)result.AsyncState;

            int rec = socket.EndReceive(result);

            //Call for Client Disconnection
            if (rec == 0)
            {
                ClientDisconnect(socket);
                return;
            }

            string message = Encoding.ASCII.GetString(buffer, 0, rec);

            //If message is quit notify other client that connection is closing
            if (message.ToLower() == "quit")
            {
                if (socket == client1TCP && client2TCP != null)
                {
                    byte[] sendBuffer = Encoding.ASCII.GetBytes("Client 1 has quit, Connection closing...");
                    client2TCP.BeginSend(sendBuffer, 0, sendBuffer.Length, 0, new AsyncCallback(SendCallback), client2TCP);
                    Console.WriteLine("Received 'quit' from Client 1, notifying Client 2...");
                }
                else if (socket == client2TCP && client1TCP != null)
                {
                    byte[] sendBuffer = Encoding.ASCII.GetBytes("Client 2 has quit, Connection closing...");
                    client1TCP.BeginSend(sendBuffer, 0, sendBuffer.Length, 0, new AsyncCallback(SendCallback), client1TCP);
                    Console.WriteLine("Received 'quit' from Client 2, notifying Client 1...");
                }

                //Close TCP connection 
                ClientDisconnect(socket);
                return;
            }

            //Sending message to other client 
            if (socket == client1TCP && client2TCP != null)
            {
                if (message.Contains("Name:"))
                {
                    Console.WriteLine($"Client 1 {message}");
                    string[] s = message.Split(' ');
                    client1Name = s[1];
                    Console.WriteLine($"the client 1 name is: {client1Name}");
                }
                else if (message.Contains("Score:"))
                {
                    Console.WriteLine($"Client 1 {message}");
                    string[] s = message.Split(' ');
                    client1Score = float.Parse(s[1]);                    
                }
                else
                {
                    byte[] sendBuffer = Encoding.ASCII.GetBytes(message);
                    client2TCP.BeginSend(sendBuffer, 0, sendBuffer.Length, 0, new AsyncCallback(SendCallback), client2TCP);
                    Console.WriteLine("Received Message '" + message + "'" + " from " + client1TCP.RemoteEndPoint + " -> Sent to " + client2TCP.RemoteEndPoint);
                }
            }
            else if (socket == client2TCP && client1TCP != null)
            {
                if (message.Contains("Name:"))
                {
                    Console.WriteLine($"Client 2 {message}");
                    string[] s = message.Split(' ');
                    client2Name = s[1];

                    Console.WriteLine($"the client 2 name is: {client2Name}");
                }
                else if (message.Contains("Score:"))
                {
                    Console.WriteLine($"Client 2 {message}");
                    string[] s = message.Split(' ');
                    client2Score = float.Parse(s[1]);
                }
                else
                {
                    byte[] sendBuffer = Encoding.ASCII.GetBytes(message);
                    client1TCP.BeginSend(sendBuffer, 0, sendBuffer.Length, 0, new AsyncCallback(SendCallback), client1TCP);
                    Console.WriteLine("Received Message '" + message + "'" + " from " + client2TCP.RemoteEndPoint + " -> Sent to " + client1TCP.RemoteEndPoint);
                }
            }

            socket.BeginReceive(buffer, 0, buffer.Length, 0, new AsyncCallback(ReceiveTCPCallback), socket);
        }

        private static void SendCallback(IAsyncResult result)
        {
            Socket socket = (Socket)result.AsyncState;
            socket.EndSend(result);
        }

        //Position Updates / UDP
        private static void ReceiveUDPData()
        {
            byte[] UDPBuffer = new byte[1024];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    int recPos = UDPServer.ReceiveFrom(UDPBuffer, ref remoteEP);
                    IPEndPoint senderEndPoint = (IPEndPoint)remoteEP;

                    //Checks if either client is Null, if so it will look for reconnection 
                    if (client1UDP == null || client2UDP == null)
                    {
                        ClientReconnect(senderEndPoint);
                        continue;
                    }

                    //Checks that both UDP clients are not null
                    if (client1UDP != null && client2UDP != null)
                    {
                        Buffer.BlockCopy(UDPBuffer, 0, clientPosVeloData, 0, 24);

                        //Sends position data to the other client
                        if (senderEndPoint.Equals(client1UDP))
                        {
                            UDPServer.SendTo(UDPBuffer, recPos, SocketFlags.None, client2UDP);
                            Console.WriteLine("Position Received X:" + clientPosVeloData[0] + " Y:" + clientPosVeloData[1] + " Z:" + clientPosVeloData[2] + " And Velocity X:" + clientPosVeloData[3] + " Y:" + clientPosVeloData[4] + " Z:" + clientPosVeloData[5] + " from " + client1UDP + " -> Sent To " + client2UDP);
                        }
                        else if (senderEndPoint.Equals(client2UDP))
                        {

                            UDPServer.SendTo(UDPBuffer, recPos, SocketFlags.None, client1UDP);
                            Console.WriteLine("Position Received X:" + clientPosVeloData[0] + " Y:" + clientPosVeloData[1] + " Z:" + clientPosVeloData[2] + " And Velocity X:" + clientPosVeloData[3] + " Y:" + clientPosVeloData[4] + " Z:" + clientPosVeloData[5] + " from " + client2UDP + " -> Sent To " + client1UDP);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("UDP Error: " + e.Message);
                }
            }
        }

        private static void ClientReconnect(IPEndPoint senderEndPoint)
        {
            //Check to see if client1UDP is null and the senders end point is not equal to client2UDP to avoid client2UDP registering as client1UDP
            if (client1UDP == null && !senderEndPoint.Equals(client2UDP))
            {
                client1UDP = senderEndPoint;
                Console.WriteLine("Registered Client 1 Using UDP: " + client1UDP);
            }
            //Check to see if client2UDP is null and the senders end point is not equal to client1UDP to avoid client1UDP registering as client2UDP
            else if (client2UDP == null && !senderEndPoint.Equals(client1UDP))
            {
                client2UDP = senderEndPoint;
                Console.WriteLine("Registered Client 2 Using UDP: " + client2UDP);
            }
            //Prints Connections Full if more than 2 client endPoints
            else
            {
                Console.WriteLine("Connections Full: " + senderEndPoint);
            }
        }

        private static void ClientDisconnect(Socket clientSocket)
        {
            //Checks what client TCP disconnected and resets client to null 
            if (clientSocket == client1TCP)
            {
                Console.WriteLine("Client 1 TCP disconnected.");
                client1TCP = null;
                client1UDP = null;

                //save to file here
                SaveLeaderboardData(client1Score, $"{client1Name}: {client1Score}");
            }
            else if (clientSocket == client2TCP)
            {
                Console.WriteLine("Client 2 TCP disconnected.");
                client2TCP = null;
                client2UDP = null;

                // save to file here
                SaveLeaderboardData(client2Score, $"{client2Name}: {client2Score}");
            }

            clientSocket.Close();
        }

        private static ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random());

        private static void StartCoinSpawning()
        {
            Thread coinThread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(5000); // Spawn a coin every 5 seconds

                    float x = (float)(random.Value.NextDouble() * 10 - 5); // Random X position
                    float y = 0.5f; // Fixed height
                    float z = (float)(random.Value.NextDouble() * 10 - 5); // Random Z position

                    byte[] coinData = Encoding.ASCII.GetBytes($"coin_spawn,{x},{y},{z}");

                    // Send coin spawn message to both clients
                    if (client1UDP != null)
                    {
                        UDPServer.SendTo(coinData, SocketFlags.None, client1UDP);
                    }
                    if (client2UDP != null)
                    {
                        UDPServer.SendTo(coinData, SocketFlags.None, client2UDP);
                    }

                    Console.WriteLine($"Spawned Coin at: X={x}, Y={y}, Z={z}");
                }
            });

            coinThread.IsBackground = true;
            coinThread.Start();
        }

        private static void SaveLeaderboardData(float score, string lineToAdd)
        {
            if (File.Exists(filePath))
            {
                string[] allLines = File.ReadAllLines(filePath);
                foreach (string line in allLines)
                {
                    string[] s = line.Split(' ');
                    float scoreNum = float.Parse(s[1]);

                    if (score > scoreNum)
                    {
                        for (int i = allLines.Length - 1; i > Array.IndexOf(allLines, line) + 1; i--)
                        {
                            try
                            {
                                allLines[i] = allLines[i - 1];
                            }
                            catch (IndexOutOfRangeException e)
                            {
                                Console.WriteLine("array is out of index");
                            }
                        }
                        
                        allLines[Array.IndexOf(allLines, line)] = lineToAdd;
                        break;
                    }
                }

                File.WriteAllLines(filePath, allLines);
            }
            else
            {
                Console.WriteLine("file does not exist");
            }
        }
    }
}
