using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace LaserAdd.SmallPrinter
{
    public sealed class SliceTask
    {
        public string Name { get; internal set; }
        public string DirectoryPath { get; internal set; }
        public IList<string> SliceFiles { get; internal set; }
        public int WidthPixels { get; internal set; }
        public int HeightPixels { get; internal set; }
    }

    public static class SliceTaskLoader
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".bmp", ".tif", ".tiff", ".jpg", ".jpeg"
        };

        public static SliceTask LoadDirectory(string directoryPath, SmallPrinterProfile profile, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(directoryPath)) throw new ArgumentNullException("directoryPath");
            if (profile == null) throw new ArgumentNullException("profile");
            string fullPath = Path.GetFullPath(directoryPath);
            if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException("切片任务目录不存在：" + fullPath);

            List<string> files = Directory.EnumerateFiles(fullPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => Path.GetFileName(path), NaturalFileNameComparer.Instance)
                .ToList();
            if (files.Count == 0)
                throw new InvalidDataException("所选目录中没有支持的切片图。支持 PNG、BMP、TIF、TIFF、JPG、JPEG。");

            int width = 0;
            int height = 0;
            for (int index = 0; index < files.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using (Image image = Image.FromFile(files[index], false))
                    {
                        if (index == 0) { width = image.Width; height = image.Height; }
                        if (image.Width != width || image.Height != height)
                            throw new InvalidDataException("切片尺寸不一致：" + Path.GetFileName(files[index]) +
                                " 为 " + image.Width + "×" + image.Height + " px，首层为 " + width + "×" + height + " px。");
                    }
                }
                catch (InvalidDataException) { throw; }
                catch (Exception exception)
                {
                    throw new InvalidDataException("无法读取切片图：" + Path.GetFileName(files[index]) + "；" + exception.Message, exception);
                }
            }

            RasterValidator.ValidateOrThrow(profile, width, height);
            return new SliceTask
            {
                Name = new DirectoryInfo(fullPath).Name,
                DirectoryPath = fullPath,
                SliceFiles = files.AsReadOnly(),
                WidthPixels = width,
                HeightPixels = height
            };
        }

        private sealed class NaturalFileNameComparer : IComparer<string>
        {
            internal static readonly NaturalFileNameComparer Instance = new NaturalFileNameComparer();

            public int Compare(string left, string right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return -1;
                if (right == null) return 1;
                int leftIndex = 0, rightIndex = 0;
                while (leftIndex < left.Length && rightIndex < right.Length)
                {
                    if (Char.IsDigit(left[leftIndex]) && Char.IsDigit(right[rightIndex]))
                    {
                        int leftEnd = leftIndex, rightEnd = rightIndex;
                        while (leftEnd < left.Length && Char.IsDigit(left[leftEnd])) leftEnd++;
                        while (rightEnd < right.Length && Char.IsDigit(right[rightEnd])) rightEnd++;
                        string leftNumber = left.Substring(leftIndex, leftEnd - leftIndex).TrimStart('0');
                        string rightNumber = right.Substring(rightIndex, rightEnd - rightIndex).TrimStart('0');
                        if (leftNumber.Length == 0) leftNumber = "0";
                        if (rightNumber.Length == 0) rightNumber = "0";
                        int lengthResult = leftNumber.Length.CompareTo(rightNumber.Length);
                        if (lengthResult != 0) return lengthResult;
                        int numberResult = String.CompareOrdinal(leftNumber, rightNumber);
                        if (numberResult != 0) return numberResult;
                        int digitCountResult = (leftEnd - leftIndex).CompareTo(rightEnd - rightIndex);
                        if (digitCountResult != 0) return digitCountResult;
                        leftIndex = leftEnd;
                        rightIndex = rightEnd;
                        continue;
                    }

                    int characterResult = Char.ToUpperInvariant(left[leftIndex]).CompareTo(Char.ToUpperInvariant(right[rightIndex]));
                    if (characterResult != 0) return characterResult;
                    leftIndex++;
                    rightIndex++;
                }
                return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
            }
        }
    }
}
