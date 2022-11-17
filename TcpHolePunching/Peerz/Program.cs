using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TcpHolePunching;
using TcpHolePunching.Messages;
using static System.Net.Mime.MediaTypeNames;

namespace Peer
{
    public class Program
    {
        private static NetworkPeer IntroducerSocket { get; set; }
        private static NetworkPeer ListenSocket { get; set; }
        private static NetworkPeer ConnectSocketInternal { get; set; }
        private static NetworkPeer ConnectSocketExternal { get; set; }

        private static int PORT = new Random().Next(49155, 64000);

        enum ConnectionType
        {
            Listener,
            Public,
            Private,
            None
        }

        private static ConnectionType connectionType = ConnectionType.None;

        static void Main(string[] args)
        {
            Console.Title = "Peer - TCP Hole Punching Proof of Concept";



            IntroducerSocket = new NetworkPeer();
            IntroducerSocket.OnConnectionAccepted += Peer_OnConnectionAccepted;
            IntroducerSocket.OnConnectionSuccessful += PeerOnConnectionSuccessful;
            IntroducerSocket.OnMessageSent += PeerOnMessageSent;
            IntroducerSocket.OnMessageReceived += Peer_OnMessageReceived;
            IntroducerSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));

            for (int i = 0; i < 60000; i++)
            {

            }


            string input = "34.126.115.248:9999";//(String.IsNullOrEmpty(input)) ? "50.18.245.235:1618" : input;

            // string input = "112.163.241.175:9999";
            // string input = "112.163.241.175:1618";

            var introducerEndpoint = input.Parse();

            Console.WriteLine(String.Format("Connecting to the Introducer at {0}:{1}...", introducerEndpoint.Address, introducerEndpoint.Port));
            IntroducerSocket.Connect(introducerEndpoint.Address, introducerEndpoint.Port);



            




            while (true) {}
        }

        static void Peer_OnConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {

        }

        static void PeerOnConnectionSuccessful(object sender, ConnectionAcceptedEventArgs e)
        {
            Console.WriteLine();
            Console.WriteLine("Requesting to register with the Introducer...");
            IntroducerSocket.Send(new RequestIntroducerRegistrationMessage() { InternalClientEndPoint = (IPEndPoint)e.Socket.LocalEndPoint });
        }

        static void PeerOnMessageSent(object sender, MessageSentEventArgs e)
        {
        }

        static void StartCommandLine()
        {
            while (true)
            {
                Console.WriteLine("A키를 누르면 서버와 연결된 피어에게 랜덤 에코 메시지 전송함");


                if (Console.ReadKey().Key == ConsoleKey.A)
                {
                    string msg = Guid.NewGuid().ToString();
                    var echoMsg = new TextMessage() { Message = new Random().Next(0, int.MaxValue) };

                    if (connectionType == ConnectionType.Listener)
                        ListenSocket.SendToPeer(echoMsg);
                    else if (connectionType == ConnectionType.Public)
                        ConnectSocketExternal.Send(echoMsg);
                    else if (connectionType == ConnectionType.Private)
                        ConnectSocketInternal.Send(echoMsg);
                    else
                        Debug.Assert(false);

                    IntroducerSocket.Send(echoMsg);
                    Console.WriteLine("보냄");
                }
            }
        }

        static void Peer_OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            switch (e.MessageType)
            {
                case MessageType.ResponseIntroducerRegistration:
                {
                    ListenSocket = new NetworkPeer();
                    ListenSocket.OnConnectionAccepted += (s, e1) =>
                    {
                        connectionType = ConnectionType.Listener;
                        Console.WriteLine($"{e1.Socket.RemoteEndPoint}가 당신의 Listener 소켓과 연결되었습니다.");
                    };

                    ListenSocket.OnPeerMessageReceived += ListenerMessageReceived;
                    ListenSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
                    // ListenSocket.Bind(IntroducerSocket.Socket.LocalEndPoint as IPEndPoint);
                    ListenSocket.Listen();
                    // Console.WriteLine("리스닝 소켓 로컬 포인트: " + ListenSocket.Socket.LocalEndPoint);
                    Console.WriteLine("리스닝 소켓 로컬 포인트: " + new IPEndPoint(IPAddress.Any, PORT));

                        var message = new ResponseIntroducerRegistrationMessage();
                    message.ReadPayload(e.MessageReader);

                    Console.WriteLine(String.Format("Introducer: You have been registered as \"{0}\".", message.RegisteredEndPoint));

                    Console.Write("Endpoint of your peer: ");

                    var peerEndPoint = Console.ReadLine().Parse();

                    Console.WriteLine(String.Format("Requesting an introduction to {0}:{1}...", peerEndPoint.Address, peerEndPoint.Port));
                    IntroducerSocket.Send(new RequestIntroducerIntroductionMessage() { InternalOwnEndPoint = (IPEndPoint)IntroducerSocket.Socket.LocalEndPoint, ExternalPeerEndPoint = peerEndPoint });

                    
                    
                    }
                break;
                case MessageType.ResponseIntroducerIntroduction:
                {
                    var message = new ResponseIntroducerIntroductionMessage();
                    message.ReadPayload(e.MessageReader);

                    Console.WriteLine(String.Format("Introducer: Your peer's internal endpoint is \"{0}\".", message.InternalPeerEndPoint));
                    Console.WriteLine(String.Format("Introducer: Your peer's external endpoint is \"{0}\".", message.ExternalPeerEndPoint));


                        


                        ConnectSocketExternal = new NetworkPeer();
                    ConnectSocketExternal.Bind(new IPEndPoint(IPAddress.Any, PORT));
                    Console.WriteLine(String.Format("Connecting to your peer's external endpoint..."));
                    ConnectSocketExternal.OnMessageReceived += ExternelMessageReceived;
                    ConnectSocketExternal.OnConnectionSuccessful += (s, e1) =>
                    {
                        connectionType = ConnectionType.Public;
                        Console.WriteLine("상대 피어와 Public 주소로 연결 되었습니다.");
                        StartCommandLine();
                    };
                    ConnectSocketExternal.Connect(message.ExternalPeerEndPoint.Address, message.ExternalPeerEndPoint.Port);

                    ConnectSocketInternal = new NetworkPeer();
                    ConnectSocketInternal.Bind(new IPEndPoint(IPAddress.Any, PORT));
                    Console.WriteLine(String.Format("Connecting to your peer's internal endpoint..."));
                    ConnectSocketInternal.OnConnectionSuccessful += (s, e1) =>
                    {
                        connectionType = ConnectionType.Private;
                        Console.WriteLine("상대 피어와 Private 주소로 연결 되었습니다.");
                        StartCommandLine();
                    };
                    ConnectSocketInternal.OnMessageReceived += InternalMessageReceived;
                    ConnectSocketInternal.Connect(message.InternalPeerEndPoint.Address, message.InternalPeerEndPoint.Port);




                        Console.WriteLine(String.Format("Listening for clients on {0}...", ListenSocket.Socket.LocalEndPoint));
                        break;
                }
                case MessageType.TextMessage:
                {
                    var message = new TextMessage();
                    message.ReadPayload(e.MessageReader);
                    Console.WriteLine("Introducer로부터 메시지 수신: " + message.Message);

                    if (!message.Echo)
                    {
                        message.Echo = true;
                        ConnectSocketExternal.Send(message);
                    }

                }
                break;
            }
        }

        private static void ExternelMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            switch (e.MessageType)
            {
                case MessageType.TextMessage:
                {
                    var message = new TextMessage();
                    message.ReadPayload(e.MessageReader);
                    Console.WriteLine("Externel 소켓에서 메시지 수신: " + message.Message);

                    if (!message.Echo)
                    {
                        message.Echo = true;
                        ConnectSocketExternal.Send(message);
                    }
                       
                }
                break;
            }
        }


        private static void InternalMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            switch (e.MessageType)
            {
                case MessageType.TextMessage:
                {
                    var message = new TextMessage();
                    message.ReadPayload(e.MessageReader);
                    Console.WriteLine("Internel 소켓에서 메시지 수신: " + message.Message);

                    if (!message.Echo)
                    {
                        message.Echo = true;
                        ConnectSocketInternal.Send(message);
                    }
                    
                }
                    break;
            }
        }


        private static void ListenerMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            switch (e.MessageType)
            {
                case MessageType.TextMessage:
                {
                    var message = new TextMessage();
                    message.ReadPayload(e.MessageReader);
                    Console.WriteLine("Listener 소켓에서 메시지 수신: " + message.Message);

                    if (!message.Echo)
                    {
                        message.Echo = true;
                        ListenSocket.SendToPeer(message);
                        Console.WriteLine("에코 송신함");
                    }
                }
                break;
            }
        }
    }
}
