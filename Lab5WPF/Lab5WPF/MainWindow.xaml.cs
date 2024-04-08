using System;
using System.Configuration;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Lab5WPF
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool done = true;
        private UdpClient udpClient;

        //адреса портов и ttl, из конфигурации приложения
        private IPAddress groupAddress;
        private int localPort;
        private int remotePort;
        private int ttl;

        //удаленная точка получения сообщений
        private IPEndPoint remoteEndPoint;
        //кодировка сообщений
        private UnicodeEncoding encoding = new UnicodeEncoding();

        //данные сообщения
        private string name;
        private string message;

        //синхронизация
        private readonly SynchronizationContext _syncContext;

        //загрузка главного окна
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                //выгрузка параметров из конфигурации приложения 
                NameValueCollection configuration = ConfigurationSettings.AppSettings;
                groupAddress = IPAddress.Parse(configuration["GroupAddress"]);
                localPort = int.Parse(configuration["LocalPort"]);
                remotePort = int.Parse(configuration["RemotePort"]);
                ttl = int.Parse(configuration["TTL"]);
            }
            catch
            {
                MessageBox.Show(this, "Error in app config file!", "Error Multicast chart", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            _syncContext = SynchronizationContext.Current;
        }

        //присоединиться к чату
        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            //параметры пользователя
            name = textName.Text;
            textName.IsReadOnly = true;

            try
            {
                //создание нового клиента
                udpClient = new UdpClient(localPort);
                //присоединение к группе
                udpClient.JoinMulticastGroup(groupAddress, ttl);

                //обеспечение получения сообщений из группы
                remoteEndPoint = new IPEndPoint(groupAddress, remotePort);
                Thread reciever = new Thread(new ThreadStart(Listener));

                reciever.IsBackground = true;
                reciever.Start();

                //отправка сообщения о присоединении к чату
                byte[] data = encoding.GetBytes(name + " has joined the chat");
                udpClient.Send(data, data.Length, remoteEndPoint);

                //(раз)блокировка необходимых кнопок
                buttonStart.IsEnabled = false;
                buttonStop.IsEnabled = true;
                buttonSend.IsEnabled = true;
            }
            catch (SocketException ex)
            {
                MessageBox.Show(this, ex.Message, "Error Multicast Chart", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //получение сообщений в чате
        private void Listener()
        {
            done = false;

            try
            {
                while (!done)
                {
                    IPEndPoint ep = null;
                    byte[] buffer = udpClient.Receive(ref ep);
                    message = encoding.GetString(buffer);

                    _syncContext.Post(o => DisplayRecievedMessage(), null);
                }
            }
            catch (Exception ex)
            {
                if (done)
                {
                    return;
                }
                else
                {
                    MessageBox.Show(this, ex.Message, "Error Multicast Chat", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        //вывод полученного сообщения
        private void DisplayRecievedMessage()
        {
            //время сообщения - из системы
            string time = DateTime.Now.ToString("t");
            //вывод сообщения в текстблок
            textMessages.Text = $"{time} {message} \r\n {textMessages.Text}";
        }

        //отправка сообщения 
        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //формирование сообщения
                byte[] data = encoding.GetBytes($"{name}: {textMessage.Text}");
                //отправка
                udpClient.Send(data, data.Length, remoteEndPoint);

                //отображение
                //время сообщения - из системы
                string time = DateTime.Now.ToString("t");
                //вывод сообщения в текстблок
                textMessages.Text = $"{time} {encoding.GetString(data)} \r\n {textMessages.Text}";

                //очистка поля ввода сообщения
                textMessage.Clear();
                textMessage.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error Multicast Chat", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //вызод из чата
        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            StopListener();
        }

        //остановка получения сообщений
        private void StopListener()
        {
            //отправка сообщения о выходе из чата
            byte[] data = encoding.GetBytes($"{name} has left the chat\n");
            udpClient.Send(data, data.Length, remoteEndPoint);

            //вызод из чата
            udpClient.DropMulticastGroup(groupAddress);
            udpClient.Close();

            done = true;

            //(раз)блокировка кнопок
            buttonStart.IsEnabled = true;
            buttonStop.IsEnabled = false;
            buttonSend.IsEnabled = false;
            textName.IsReadOnly = false;
        }

        //закрытие окна
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!done)
            {
                //остановка участия в чате
                StopListener();
            }
        }
    }
}