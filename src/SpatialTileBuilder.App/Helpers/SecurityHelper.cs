namespace SpatialTileBuilder.App.Helpers;

using System;
using System.Security.Cryptography;
using System.Text;

public static class SecurityHelper
{
    public static byte[] EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return Array.Empty<byte>();
        
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            // Using CurrentUser scope so only the user running the app can decrypt
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    public static string DecryptString(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length == 0) return string.Empty;

        try
        {
            byte[] data = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return string.Empty;
        }
    }
}
