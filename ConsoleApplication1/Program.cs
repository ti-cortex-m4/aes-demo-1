using System;
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
            new Program().Authorize();
        }

        private void ModbusTransaction1(byte[] frame, ref int count)
        {
            Console.Write("Send    ");
            Write(frame, count);

            byte[] data1 =
            {
                0x01, 0x64, 0x10, 0x90, 0x0B, 0x9B, 0xEF, 0x21, 0x4E, 0xD4, 0xB5, 0xCA, 0xB6, 0xF1, 0x93, 0xFB, 0x31,
                0x0E, 0x84, 0x60, 0xE2
            };
            data1.CopyTo(frame, 0);
            count = 21;

            Console.Write("Receive ");
            Write(frame, count);
        }


        private void ModbusTransaction2(byte[] frame, ref int count)
        {
            Console.Write("Send    ");
            Write(frame, count);

            byte[] data2 = {0x01, 0x65, 0x00, 0x00, 0x00, 0x09, 0x8D, 0xC4};
            data2.CopyTo(frame, 0);
            count = 8;

            Console.Write("Receive ");
            Write(frame, count);
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
            MD5 md5 = MD5.Create();
            byte[] aesKey = md5.ComputeHash(Encoding.Default.GetBytes(pass));
            byte[] authReq = new byte[16];

            Aes aes = Aes.Create();
            aes.KeySize = 128;
            aes.BlockSize = 128;
            aes.Padding = PaddingMode.None;
            aes.Mode = CipherMode.ECB;
            ICryptoTransform encrypter = aes.CreateEncryptor(aesKey, null);
            encrypter.TransformBlock(authKey, 0, 16, authReq, 0);

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

        private void Write(byte[] bytes, int size)
        {
            for (byte i = 0; i < size; i++)
            {
                Console.Write(bytes[i].ToString("X2") + ' ');
            }

            Console.WriteLine();
        }
    }
}