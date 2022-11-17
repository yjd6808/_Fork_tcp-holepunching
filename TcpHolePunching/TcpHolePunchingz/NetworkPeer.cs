using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TcpHolePunching.Messages;

namespace TcpHolePunching
{

    public class NetworkPeer : NetworkClient
    {
        public Socket PeerSocket { get; private set; }
        public byte[] PeerBuffer { get; private set; }

        public event EventHandler<ConnectionAcceptedEventArgs> OnConnectionAccepted;

        public event EventHandler<MessageReceivedEventArgs> OnPeerMessageReceived;
        public event EventHandler<MessageSentEventArgs> OnPeerMessageSent;

        public NetworkPeer() : base()
        {
            PeerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            PeerBuffer = new byte[1024];
        }

        /// <summary>
        /// Only binds without listening.
        /// </summary>
        public void Bind(EndPoint on)
        {
            Socket.Bind(on);
        }

        public void Listen()
        {
            Socket.Listen(Int32.MaxValue);
            Task_BeginAccepting();
        }

        private void Task_BeginAccepting()
        {
            var task = Task.Factory.FromAsync<Socket>(Socket.BeginAccept, Socket.EndAccept, null);
            task.ContinueWith(nextTask =>
            {
                Task_OnConnectionAccepted(task.Result);
                Task_BeginAccepting(); // Listen for another connection
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        // NetworkPeer가 리스너 소켓으로 동작할때만 사용할 것
        public void SendToPeer(MessageBase messageBase)
        {
            // If the registrant exists
            if (PeerSocket.Connected)
            {
                var data = messageBase.GetBytes();
                var task = Task.Factory.FromAsync<Int32>(PeerSocket.BeginSend(data, 0, data.Length, SocketFlags.None, null, PeerSocket), PeerSocket.EndSend);
                task.ContinueWith(nextTask => Task_PeerOnSendCompleted(task.Result, data.Length, PeerSocket.RemoteEndPoint, messageBase.MessageType), TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        }
       
        private void Task_PeerOnSendCompleted(int numBytesSent, int expectedBytesSent, EndPoint to, MessageType messageType)
        {
            if (numBytesSent != expectedBytesSent)
                Console.WriteLine(String.Format("Warning: Expected to send {0} bytes but actually sent {1}!",
                    expectedBytesSent, numBytesSent));

            Console.WriteLine(String.Format("리스너소켓에서 피어에게 {0} 바이트의 {1}메시지를 {2}에게 전송했습니다.", numBytesSent, messageType, to));

            if (OnPeerMessageSent != null)
                OnPeerMessageSent(this, new MessageSentEventArgs() { Length = numBytesSent, To = to });
        }

        private void Task_OnConnectionAccepted(Socket socket)
        {
            Console.WriteLine(String.Format("Connection to {0} accepted.", socket.RemoteEndPoint));

            PeerSocket = socket;

            if (OnConnectionAccepted != null)
                OnConnectionAccepted(this, new ConnectionAcceptedEventArgs() { Socket = socket} );

            Task_PeerBeginReceive();
        }

        private void Task_PeerBeginReceive()
        {
            var task = Task.Factory.FromAsync<Int32>(PeerSocket.BeginReceive(PeerBuffer, 0, PeerBuffer.Length, SocketFlags.None, null, null), Socket.EndReceive);
            task.ContinueWith(nextTask =>
            {
                try
                {
                    Task_PeerOnReceiveCompleted(task.Result);
                    Task_PeerBeginReceive(); // Receive more data
                }
                catch (Exception ex)
                {
                    var exceptionMessage = (ex.InnerException != null) ? ex.InnerException.Message : ex.Message;
                    Console.WriteLine(exceptionMessage);
                    ShutdownAndClose();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private void Task_PeerOnReceiveCompleted(int numBytesRead)
        {
            // Build back our MessageReader
            var reader = new BufferValueReader(PeerBuffer);
            var message = new Message();
            message.ReadPayload(reader);
            reader.Position = 0;

            Console.WriteLine(String.Format("연결된 P2P 피어{2}로부터 {0}바이트 {1}메시지 수신", numBytesRead, message.MessageType, Socket.RemoteEndPoint));
            OnPeerMessageReceived?.Invoke(this, new MessageReceivedEventArgs() { From = (IPEndPoint)PeerSocket.RemoteEndPoint, MessageReader = reader, MessageType = message.MessageType });
        }
    }
}
