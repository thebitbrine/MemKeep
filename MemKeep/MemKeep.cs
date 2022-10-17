using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace MemKeep
{
    public static class ExtensionOperation
    {
        public static T Retrive<T>(this T obj, [System.Runtime.CompilerServices.CallerArgumentExpression("obj")] string objectName = "", bool useBackup = false)
        {
            var Name = System.Reflection.Assembly.GetCallingAssembly().GetName().Name;

            var KeepPath = Path.Combine(Path.Combine("MemKeep", Name, "Storage"));
            Directory.CreateDirectory(KeepPath);

            var BackupPath = Path.Combine(Path.Combine("MemKeep", Name, "Backup"));
            Directory.CreateDirectory(BackupPath);

            var LogPath = Path.Combine("MemKeep", Name, $"{Name}.log.txt");
            try
            {
                var id = "";
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(obj.GetType().ToString().ToArray());
                    byte[] hashBytes = md5.ComputeHash(inputBytes);

                    id = $"{objectName}-{BitConverter.ToString(hashBytes).Replace("-", "")}.keep";
                }

                var MainFilePath = Path.Combine(KeepPath, id);
                if (useBackup)
                    MainFilePath = Path.Combine(BackupPath, id);

                var CompressedData = File.ReadAllBytes(MainFilePath);
                byte[] SerializedData;
                using (var source = new MemoryStream(CompressedData))
                {
                    byte[] lengthBytes = new byte[4];
                    source.Read(lengthBytes, 0, 4);

                    var length = BitConverter.ToInt32(lengthBytes, 0);
                    using (var decompressionStream = new GZipStream(source,
                        CompressionMode.Decompress))
                    {
                        var result = new byte[length];
                        decompressionStream.Read(result, 0, length);
                        SerializedData = result;
                    }
                }
                MemoryStream ms = new MemoryStream(SerializedData);
                ms.Seek(0, 0);
                BinaryFormatter bf = new BinaryFormatter();
                return (T)bf.Deserialize(ms);
            }
            catch (Exception ex)
            {
                File.AppendAllText(LogPath, $"{DateTime.UtcNow} (UTC)\r\nRetrive: {ex.Message}\r\n{ex.StackTrace}\r\n\r\n");
                if(!useBackup)
                    return obj.Retrive(useBackup: true);
            }
            return default(T);
        }        


        public static void Store(this object obj, [System.Runtime.CompilerServices.CallerArgumentExpression("obj")] string objectName = "")
        {
            var Name = System.Reflection.Assembly.GetCallingAssembly().GetName().Name;

            var KeepPath = Path.Combine(Path.Combine("MemKeep", Name, "Storage"));
            Directory.CreateDirectory(KeepPath);

            var BackupPath = Path.Combine(Path.Combine("MemKeep", Name, "Backup"));
            Directory.CreateDirectory(BackupPath);

            var LogPath = Path.Combine("MemKeep", Name, $"{Name}.log.txt");
            try
            {
                var id = "";
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(obj.GetType().ToString().ToArray());
                    byte[] hashBytes = md5.ComputeHash(inputBytes);

                    id = $"{objectName}-{BitConverter.ToString(hashBytes).Replace("-", "")}.keep";
                }

                var MainFilePath = Path.Combine(KeepPath, id);
                var BackupFilePath = Path.Combine(BackupPath, id);

                BinaryFormatter formatter = new BinaryFormatter();
                MemoryStream ms = new MemoryStream();
                formatter.Serialize(ms, obj);
                byte[] input = ms.ToArray();
                byte[] bytes;

                using (var result = new MemoryStream())
                {
                    var lengthBytes = BitConverter.GetBytes(input.Length);
                    result.Write(lengthBytes, 0, 4);

                    using (var compressionStream = new GZipStream(result, CompressionMode.Compress))
                    {
                        compressionStream.Write(input, 0, input.Length);
                        compressionStream.Flush();

                    }
                    bytes = result.ToArray();
                }

                FileStream fs = null;
                for (int numTries = 0; numTries < 100; numTries++)
                {
                    try
                    {
                        fs = new FileStream(MainFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        fs.Write(bytes, 0, bytes.Length);
                        fs.Close();
                    }
                    catch (IOException)
                    {
                        if (fs != null)
                        {
                            fs.Dispose();
                        }
                        Thread.Sleep(100);
                    }
                }

                for (int numTries = 0; numTries < 100; numTries++)
                {
                    try
                    {
                        fs = new FileStream(BackupFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        fs.Write(bytes, 0, bytes.Length);
                        fs.Close();
                    }
                    catch (IOException)
                    {
                        if (fs != null)
                        {
                            fs.Dispose();
                        }
                        Thread.Sleep(100);
                    }
                }
            }
            catch(Exception ex)
            {
                File.AppendAllText(LogPath, $"{DateTime.UtcNow} (UTC)\r\n{ex.Message}\r\n{ex.StackTrace}\r\n\r\n");
            }
        }
    }
}
