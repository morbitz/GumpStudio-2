using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Ultima
{
    public class UOSound
    {
        public string Name;
        public MemoryStream WAVEStream;

        public UOSound(string name, MemoryStream stream)
        {
            Name = name;
            WAVEStream = stream;
        }
    };

    public static class Sounds
    {
        private static readonly BinaryReader m_Index;
        private static readonly Stream m_Stream;
        private static readonly Dictionary<int, int> m_Translations;

        static Sounds()
        {
            m_Index = new BinaryReader(new FileStream(Client.GetFilePath("soundidx.mul"), FileMode.Open));
            m_Stream = new FileStream(Client.GetFilePath("sound.mul"), FileMode.Open);
            var reg = new Regex(@"(\d{1,3}) \x7B(\d{1,3})\x7D (\d{1,3})", RegexOptions.Compiled);

            m_Translations = new Dictionary<int, int>();

            string line;
            using (var reader = new StreamReader(Client.GetFilePath("Sound.def")))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if ((line = line.Trim()).Length != 0 && !line.StartsWith("#"))
                    {
                        var match = reg.Match(line);

                        if (match.Success)
                        {
                            m_Translations.Add(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
                        }
                    }
                }
            }
        }

        public static UOSound GetSound(int soundID)
        {
            if (soundID < 0)
            {
                return null;
            }

            m_Index.BaseStream.Seek(soundID * 12, SeekOrigin.Begin);

            var offset = m_Index.ReadInt32();
            var length = m_Index.ReadInt32();
            m_Index.ReadInt32(); // extra

            if (offset < 0 || length <= 0)
            {
                if (!m_Translations.TryGetValue(soundID, out soundID))
                {
                    return null;
                }

                m_Index.BaseStream.Seek(soundID * 12, SeekOrigin.Begin);

                offset = m_Index.ReadInt32();
                length = m_Index.ReadInt32();
                m_Index.ReadInt32(); // extra
            }

            if (offset < 0 || length <= 0)
            {
                return null;
            }

            var waveHeader = WaveHeader(length);

            length -= 40;

            var stringBuffer = new byte[40];
            var buffer = new byte[length];

            m_Stream.Seek(offset, SeekOrigin.Begin);
            m_Stream.Read(stringBuffer, 0, 40);
            m_Stream.Read(buffer, 0, length);

            var resultBuffer = new byte[buffer.Length + (waveHeader.Length << 2)];

            Buffer.BlockCopy(waveHeader, 0, resultBuffer, 0, waveHeader.Length << 2);
            Buffer.BlockCopy(buffer, 0, resultBuffer, waveHeader.Length << 2, buffer.Length);

            var str = System.Text.Encoding.ASCII
                .GetString(stringBuffer); // seems that the null terminator's not being properly recognized :/
            return new UOSound(str.Substring(0, str.IndexOf('\0')), new MemoryStream(resultBuffer));
        }

        private static int[] WaveHeader(int length)
        {
            /* ====================
             * = WAVE File layout =
             * ====================
             * char[4] = 'RIFF' \
             * int - chunk size |- Riff Header
             * char[4] = 'WAVE' /
             * char[4] = 'fmt ' \
             * int - chunk size |
             * short - format	|
             * short - channels	|
             * int - samples p/s|- Format header
             * int - avg bytes	|
             * short - align	|
             * short - bits p/s /
             * char[4] - data	\
             * int - chunk size | - Data header
             * short[..] - data /
             * ====================
             * */
            return new[]
            {
                0x46464952, length + 12, 0x45564157, 0x20746D66, 0x10, 0x010001, 0x5622, 0xAC44, 0x100002, 0x61746164,
                length - 24
            };
        }
    }
}