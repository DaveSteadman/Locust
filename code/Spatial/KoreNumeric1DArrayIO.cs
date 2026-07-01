// <fileheader>

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Numerics;

namespace Locust.Spatial;

public static class KoreNumeric1DArrayIO<T> where T : struct, INumber<T>
{
    // --------------------------------------------------------------------------------------------
    // MARK: CSV String
    // --------------------------------------------------------------------------------------------

    public static string ToCSVString(KoreNumeric1DArray<T> array, int decimalPlaces)
    {
        StringBuilder csvBuilder = new StringBuilder();
        string format = "F" + decimalPlaces;

        foreach (var value in array.Data)
        {
            csvBuilder.Append(Convert.ToDouble(value).ToString(format, CultureInfo.InvariantCulture));
            csvBuilder.Append(", ");
        }

        if (csvBuilder.Length > 2)
            csvBuilder.Length -= 2; // Remove the last comma and space

        return csvBuilder.ToString();
    }

    public static KoreNumeric1DArray<T> FromCSVString(string csvString)
    {
        string[] values = csvString.Trim().Split(',');
        T[] data = new T[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            data[i] = T.Parse(values[i].Trim(), CultureInfo.InvariantCulture);
        }

        return new KoreNumeric1DArray<T>(data);
    }

    // --------------------------------------------------------------------------------------------
    // MARK: CSV File
    // --------------------------------------------------------------------------------------------

    public static void SaveToCSVFile(KoreNumeric1DArray<T> array, string filePath, int decimalPlaces)
    {
        string csvString = ToCSVString(array, decimalPlaces);
        File.WriteAllText(filePath, csvString);
    }

    public static KoreNumeric1DArray<T> LoadFromCSVFile(string filePath)
    {
        string csvString = File.ReadAllText(filePath);
        return FromCSVString(csvString);
    }

    // --------------------------------------------------------------------------------------------
    // MARK: Binary File
    // --------------------------------------------------------------------------------------------

    public static void SaveToBinaryFile(KoreNumeric1DArray<T> array, string filePath)
    {
        using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
        {
            writer.Write(array.Length);
            foreach (var value in array.Data)
            {
                writer.Write(Convert.ToDouble(value));
            }
        }
    }

    public static KoreNumeric1DArray<T> LoadFromBinaryFile(string filePath)
    {
        using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
        {
            int length = reader.ReadInt32();
            T[] data = new T[length];

            for (int i = 0; i < length; i++)
            {
                data[i] = T.CreateChecked(reader.ReadDouble());
            }

            return new KoreNumeric1DArray<T>(data);
        }
    }
}
