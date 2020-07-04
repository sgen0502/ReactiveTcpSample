using System;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ReactiveSample
{
    class ReactiveTcpServer
    {
        private readonly TcpListener _listener;

        public ReactiveTcpServer(string ip, int port)
        {
            var ipAdd = System.Net.IPAddress.Parse(ip);
            _listener = new TcpListener(ipAdd, port);
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine("Listenを開始しました({0}:{1})。", ((System.Net.IPEndPoint)_listener.LocalEndpoint).Address, ((System.Net.IPEndPoint)_listener.LocalEndpoint).Port);
            
            Observable.FromAsync(_listener.AcceptTcpClientAsync)
                .ObserveOn(TaskPoolScheduler.Default)
                .SubscribeOn(TaskPoolScheduler.Default)
                .Repeat()
                .Delay(TimeSpan.FromMilliseconds(1000))
                .Subscribe(client =>
                {
                    var ns = client.GetStream();
                    
                    // 乱数を生成
                    var random = new Random(Convert.ToInt32(Guid.NewGuid().ToString("N").Substring(0, 8), 16)).Next(0, 100);
                    var bytes = BitConverter.GetBytes(random);
                    ns.Write(bytes, 0, bytes.Length);

                    //閉じる
                    ns.Close();
                    client.Close();
                    Console.WriteLine("クライアントとの接続を閉じました。");
                });
        }
    }
    
    class ReactiveEventClass
    {
        Socket ClientSocket { get; }
        public Subject<int> RecievedMessage { get; }

        public ReactiveEventClass(string ip, int port)
        {
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ClientSocket.Connect(ip, port);
            RecievedMessage = new Subject<int>();
            
            var buffer = new byte[16];
            Observable.FromAsync(_ => ClientSocket.ReceiveAsync(buffer, SocketFlags.None))
                .Repeat()
                .ObserveOn(TaskPoolScheduler.Default)
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(receiveSize =>
                {
                    if (receiveSize < 0) return;
                    
                    var receivedBytes = new byte[receiveSize];
                    Buffer.BlockCopy(buffer, 0, receivedBytes, 0, receiveSize);

                    RecievedMessage.OnNext(BitConverter.ToInt32(receivedBytes));
                }, exception => Console.WriteLine(exception));
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            var tcpServer = new ReactiveTcpServer("127.0.0.1", 1234);
            tcpServer.Start();
            
            var ec1 = new ReactiveEventClass("127.0.0.1", 1234);
            var ec2 = new ReactiveEventClass("127.0.0.1", 1234);
            var ec3 = new ReactiveEventClass("127.0.0.1", 1234);

            ec1.RecievedMessage.Subscribe(result => Console.WriteLine($"ec1: {result}"));
            ec2.RecievedMessage.Subscribe(result => Console.WriteLine($"ec2: {result}"));
            ec3.RecievedMessage.Subscribe(result => Console.WriteLine($"ec3: {result}"));

            Console.ReadLine();
        }
    }
}