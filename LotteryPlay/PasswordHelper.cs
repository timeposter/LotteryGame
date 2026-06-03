using System;
using System.Security.Cryptography;
using System.Text;

namespace LotteryPlay
{
    public static class PasswordHelper
    {
        /// <summary>生成密码哈希</summary>
        public static string CreateHash(string pwd)
        {
            if (string.IsNullOrEmpty(pwd)) return string.Empty;
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(pwd);
            var hashBytes = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        /// <summary>校验密码是否匹配</summary>
        public static bool Verify(string inputPwd, string dbHash)
        {
            if (string.IsNullOrEmpty(inputPwd) || string.IsNullOrEmpty(dbHash))
                return false;
            return CreateHash(inputPwd) == dbHash;
        }
    }
}