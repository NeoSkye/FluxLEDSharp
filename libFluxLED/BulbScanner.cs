using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace FluxLED
{
    public class BulbScanner
    {
        private const int DISCOVERY_PORT = 48899;

        private List<WifiLedBulb> m_discoveredBulbs = new List<WifiLedBulb>();

        private CancellationTokenSource m_cancelScanSource;

        public IReadOnlyList<WifiLedBulb> DiscoveredBulbs
        {
            get { return m_discoveredBulbs.AsReadOnly(); }
        }

        public delegate void BulbDiscoveryHandler(WifiLedBulb bulb);
        public event BulbDiscoveryHandler DiscoveredBulb;

        public async Task<IReadOnlyList<WifiLedBulb>> Scan(int millisecondsTimeout)
        {
            //Delete old bulb list
            m_discoveredBulbs.Clear();

            //Create UDP Client for discovery broadcast
            using (UdpClient discovery_client = new UdpClient())
            {
                //Send magic packet to get controllers to announce themselves
                IPEndPoint ip = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
                byte[] bytes = Encoding.ASCII.GetBytes("HF-A11ASSISTHREAD");
                discovery_client.Send(bytes, bytes.Length, ip);

                //Listen for their return packets
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, DISCOVERY_PORT);
                m_cancelScanSource = new CancellationTokenSource(millisecondsTimeout);

                while (true)
                {
                    //Hack in a way to allow a CancellationToken for ReceiveAsync
                    //Based heavily on https://stackoverflow.com/questions/19404199/how-to-to-make-udpclient-receiveasync-cancelable
                    var receive_task = discovery_client.ReceiveAsync();
                    var tcs = new TaskCompletionSource<bool>();
                    using (m_cancelScanSource.Token.Register(s => tcs.TrySetResult(true), null))
                    {
                        if(await Task.WhenAny(receive_task, tcs.Task) == receive_task)
                        {
                            //ReceiveAsync was successful, parse the reply
                            string message = Encoding.ASCII.GetString(receive_task.Result.Buffer);
                            string[] bulb_data = message.Split(',');
                            var bulb = new WifiLedBulb(bulb_data[0], bulb_data[1], bulb_data[2]);
                            m_discoveredBulbs.Add(bulb);
                            DiscoveredBulb?.Invoke(bulb);
                        }
                        else
                        {
                            //Cancelled (or timed out), close out socket
                            discovery_client.Close();
                            m_cancelScanSource.Dispose();
                            m_cancelScanSource = null;
                            break;
                        }
                    }
                }
            }
            return m_discoveredBulbs.AsReadOnly();
        }

        public void CancelScan()
        {
            m_cancelScanSource?.Cancel();
        }
    }
}
