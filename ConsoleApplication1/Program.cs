using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleApplication1
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            new Program().SetTime();
//            new Program().CorrectTime("1");
//            new Program().CorrectTime("2");
//            new Program().CorrectTime("10");
//            new Program().CorrectTime("-1");
//            new Program().CorrectTime("-2");
//            new Program().CorrectTime("-10");
        }

        private void ModbusTransaction1(byte[] frame, ref int count)
        {
            Console.WriteLine("Send    " + ToString(frame, count));

            byte[] data1 =
            {
                0x01, 0x64, 0x10, 0x90, 0x0B, 0x9B, 0xEF, 0x21, 0x4E, 0xD4, 0xB5, 0xCA, 0xB6, 0xF1, 0x93, 0xFB, 0x31,
                0x0E, 0x84, 0x60, 0xE2
            };
            data1.CopyTo(frame, 0);
            count = 21;

            Console.WriteLine("Receive " + ToString(frame, count));
        }


        private void ModbusTransaction2(byte[] frame, ref int count)
        {
            Console.WriteLine("Send    " + ToString(frame, count));

            byte[] data2 = {0x01, 0x65, 0x00, 0x00, 0x00, 0x09, 0x8D, 0xC4};
            data2.CopyTo(frame, 0);
            count = 8;

            Console.WriteLine("Receive " + ToString(frame, count));
        }

        private void Authorize()
        {
            byte accessLevel = 1;
            char[] pass = {'1', '1', '1', '1', '1', '1'};

            int len;
            byte[] data = new byte[256];
            byte[] getKey = {0x01, 0x64, 0x00, 0x01, 0x00, 0x08};
            len = getKey.Length;
            getKey.CopyTo(data, 0);

            ModbusTransaction1(data, ref len);
            if (len != 21)
            {
                Console.WriteLine("Авторизация не удалась!\r\nНе удалось запросить ключ авторизации.");
                return;
            }

            byte[] authKey = data.Skip(3).Take(16).ToArray();
            Console.WriteLine("authKey " + ToString(authKey));

            MD5 md5 = MD5.Create();
            byte[] rgbKey = md5.ComputeHash(Encoding.Default.GetBytes(pass));
            Console.WriteLine("rgbKey  " + ToString(rgbKey));

            byte[] authReq = new byte[16];

            Aes aes = Aes.Create();
            aes.KeySize = 128;
            aes.BlockSize = 128;
            aes.Padding = PaddingMode.None;
            aes.Mode = CipherMode.ECB;
            ICryptoTransform encrypter = aes.CreateEncryptor(rgbKey, null);
            encrypter.TransformBlock(authKey, 0, 16, authReq, 0);
            Console.WriteLine("authReq " + ToString(authReq));

            byte[] writeAuth = {0x01, 0x65, 0x00, 0x00, 0x00, 0x09, 0x12};
            writeAuth.CopyTo(data, 0);
            data[7] = (byte) accessLevel;
            data[8] = 0;
            authReq.CopyTo(data, 9);
            len = 7 + 2 + 16;
            ModbusTransaction2(data, ref len);

            if (len != 8 || ((data[1] & 0x80) != 0))
            {
                string s = "Авторизация не удалась!";
                if ((data[1] & 0x80) != 0)
                {
                    string error;
                    switch (data[2])
                    {
                        case 4:
                            error = "Введен неправильный пароль!";
                            break;
                        case 6:
                            error = "Запрошен несуществующий уровень доступа!";
                            break;
                        case 7:
                            error = "Ключ авторизации устарел!";
                            break;
                        case 8:
                            error = "Превышено число попыток ввода пароля, авторизация заблокирована!";
                            break;
                        default:
                            error = "Код ошибки: " + data[2];
                            break;
                    }

                    s += "\r\n" + error;
                }

                Console.WriteLine(s);
                return;
            }

            Console.WriteLine("Success");
        }

        private int Modbus_Request1(byte[] cmd, byte[] data)
        {
            Console.WriteLine("Send    " + ToString(cmd, cmd.Length));
            
            byte[] data1 =
            {
                0x01, 0x65, 0x00, 0x4F, 0x00, 0x04, 0x7D, 0xD6
            };
            data1.CopyTo(data, 0);
            
            return 8;
        }
        
        private void CorrectTime(string text)
        {
            Console.WriteLine("Text    " + text);
            
            double d;
            string s = text.Replace(",", CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);
            s = s.Replace(".", CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator);
            if (!double.TryParse(s, out d))
                throw new IOException("Некорректное смещение!");
            
            long delta = (long) (d * 0x100000000 + 0.5 * Math.Sign(d));
            Console.WriteLine("delta    " + delta.ToString("X16"));
            byte[] cmd =
            {
                0x01, 0x65, 0x00, 0x4F, 0x00, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            };
            
            byte[] data = new byte[256];
            BitConverter.GetBytes(delta).CopyTo(cmd, 7);
            
            if (Modbus_Request1(cmd, data) != 8 || ((data[1] & 0x80) != 0))
            {
                if (data[2] == 0x04)
                {
                    Console.WriteLine("Недостаточный уровень доступа!\r\nУстановите уровень доступа не менее 1 в настройках.");
                    return;
                }

                if (data[2] == 0x05)
                {
                    Console.WriteLine("Превышено максимальное время коррекции.Для коррекции времени можно увеличить уровень авторизации в настройках.");
                    return;
                }
                
                Console.WriteLine("Не удалось скорректировать время!");
                return;
            }

            Console.WriteLine("Время скорректировано");
        }
        
        private int Modbus_Request2(byte[] cmd, byte[] data)
        {
            Console.WriteLine("Send    " + ToString(cmd, cmd.Length));
            
            byte[] data1 =
            {
                0x01, 0x65, 0x00, 0x4F, 0x00, 0x04, 0x7D, 0xD6
            };
            data1.CopyTo(data, 0);
            
            return 8;
        }
        
        private string SetTime()
        {
            DateTime time;
//            if(rbSystemTime.Checked)
                time = DateTime.UtcNow;
//            else
//            {
//                time = dateSet.Value;
//                time = time.Add(timeSet.Value.TimeOfDay);
//                time = TimeZoneInfo.ConvertTimeToUtc(time, TimeZoneInfo);
//            }
            uint sec = (uint)time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            uint frac = (uint)((time.TimeOfDay.Ticks % TimeSpan.TicksPerSecond) * 0x100000000/ TimeSpan.TicksPerSecond);
            byte[] cmd = { 0x01, 0x65, 0x00, 0x46, 0x00, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            byte[] data = new byte[256];
            BitConverter.GetBytes(sec).CopyTo(cmd, 7);
            BitConverter.GetBytes(frac).CopyTo(cmd, 11);
            if(Modbus_Request2(cmd, data) != 8 || ((data[1] & 0x80) != 0))
            {
                if(data[2] == 0x04) throw new IOException("Недостаточный уровень доступа!\r\nУстановите уровень доступа не менее 1 в настройках.");
                if(data[2] == 0x05) throw new IOException("Превышено максимальное время коррекции. Для установки времени можно увеличить уровень авторизации в настройках.");
                throw new IOException("Не удалось установить время!");
            }
            //time = TimeZoneInfo.ConvertTime(time, TimeZoneInfo);
            return "Время установлено: " + time.ToLongTimeString() + " " + time.ToLongDateString();
        }
        
        private string ToString(byte[] bytes, int size)
        {
            string s = "";
            for (byte i = 0; i < size; i++)
            {
                s += bytes[i].ToString("X2") + ' ';
            }

            return s;
        }

        private string ToString(byte[] bytes)
        {
            string s = "";
            for (byte i = 0; i < bytes.Length; i++)
            {
                s += bytes[i].ToString("X2") + ' ';
            }

            return s;
        }
    }
}