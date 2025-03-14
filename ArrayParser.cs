using System;
using System.Collections.Generic;

public static class ArrayParser
{
    public static T Parse<T>(string input) where T : class
    {
        Type type = typeof(T);

        if (!type.IsArray)
            throw new ArgumentException("Type must be an array type.", nameof(T));

        int[] dimensions;
        string[] elements;
        ParseArray(input, out dimensions, out elements);

        Type elementType = type.GetElementType()!;
        Array array = Array.CreateInstance(elementType, dimensions);
        FillArray(array, elementType, elements);

        return array as T;
    }

    private static void ParseArray(string input, out int[] dimensions, out string[] elements)
    {
        List<int> dims = new List<int>();
        List<string> elems = new List<string>();
        int start = 0;
        int end = input.Length - 1;
        bool inArray = false;
        int depth = 0;

        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '[')
            {
                if (!inArray)
                {
                    start = i + 1;
                }
                inArray = true;
                depth++;
            }
            else if (input[i] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    string subArray = input.Substring(start, i - start);
                    ParseArray(subArray, out int[] subDims, out string[] subElems);
                    dims.Add(subDims.Length);
                    elems.AddRange(subElems);
                }
                inArray = depth > 0;
            }
            else if (input[i] == ',' && !inArray)
            {
                string element = input.Substring(start, i - start).Trim();
                elems.Add(element);
                start = i + 1;
            }
        }

        if (start < end)
        {
            string element = input.Substring(start, end - start + 1).Trim();
            elems.Add(element);
        }

        dimensions = dims.ToArray();
        elements = elems.ToArray();
    }

    private static void FillArray(Array array, Type elementType, string[] elements)
    {
        int[] indices = new int[array.Rank];

        for (int i = 0; i < elements.Length; i++)
        {
            object value = ParseValue(elementType, elements[i]);

            array.SetValue(value, indices);

            for (int j = indices.Length - 1; j >= 0; j--)
            {
                indices[j]++;
                if (indices[j] < array.GetLength(j))
                {
                    break;
                }
                indices[j] = 0;
            }
        }
    }

    private static object ParseValue(Type type, string element)
    {
        if (type.IsEnum)
        {
            return Enum.Parse(type, element);
        }
        else if (typeof(IConvertible).IsAssignableFrom(type))
        {
            return Convert.ChangeType(element, type);
        }
        else
        {
            throw new NotSupportedException($"Type {type} is not supported.");
        }
    }
}