using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace phantom.WPF.AndroidFileManagement
{
    public class Globals
    {
        public static MainWindow? MainWindow { get; set; }

        public static async Task<string> CalculateMD5Async(string filePath)
        {
            try
            {
                string md5hash;
                using (FileStream fileStream = System.IO.File.OpenRead(filePath))
                {
                    using (MD5 md5 = MD5.Create())
                    {
                        byte[] hashBytes = await Task.Run(() => md5.ComputeHash(fileStream));
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < hashBytes.Length; i++)
                        {
                            sb.Append(hashBytes[i].ToString("x2")); // To get hexadecimal string
                        }
                        md5hash= sb.ToString();
                        md5.Dispose();
                    }
                    fileStream.Close();
                    await fileStream.DisposeAsync();
                }
                return md5hash;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating MD5: {ex.Message}");
                return string.Empty; // Or throw an exception if you prefer
            }
        }
    }
}
