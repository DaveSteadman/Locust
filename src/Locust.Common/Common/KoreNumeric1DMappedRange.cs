// <fileheader>

// Kore1DMappedRange: A class of a range of input values, mapped to a list of output values.
// Allows a caller to define a range of input and outputs that are accessible through linear interpolation.

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Locust;

public class KoreNumeric1DMappedRange<T> where T : struct, INumber<T>
{
    // List to store input-output pairs
    private List<(T input, T output)> entries = new List<(T, T)>();

    // --------------------------------------------------------------------------------------------

    // Method to add a new input-output pair
    public void AddEntry(T input, T output)
    {
        entries.Add((input, output));
        // Sort the list by input value
        entries.Sort((a, b) => a.input.CompareTo(b.input));
    }

    public void Add(T input, T output)
    {
        AddEntry(input, output);
    }

    // --------------------------------------------------------------------------------------------

    // Method to get the output value corresponding to an input value using linear interpolation
    public T GetOutput(T input)
    {
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("No entries have been added.");
        }

        // If the input is outside the bounds of the known inputs, return the nearest known output
        if (input <= entries[0].input)
        {
            return entries[0].output;
        }
        if (input >= entries[entries.Count - 1].input)
        {
            return entries[entries.Count - 1].output;
        }

        // Find the two entries that the input falls between
        for (int i = 0; i < entries.Count - 1; i++)
        {
            if (input >= entries[i].input && input <= entries[i + 1].input)
            {
                // Perform linear interpolation
                T t = (input - entries[i].input) / (entries[i + 1].input - entries[i].input);
                return entries[i].output + t * (entries[i + 1].output - entries[i].output);
            }
        }

        // Should never reach this point if the list is sorted correctly
        throw new Exception("Unexpected error in GetValue.");
    }

    // --------------------------------------------------------------------------------------------

    // Method to get the input value corresponding to an output value using linear interpolation
    public T GetInput(T output)
    {
        if (entries.Count == 0)
            throw new InvalidOperationException("No entries have been added.");

        // Find the min and max outputs and their indices
        int minIndex = 0, maxIndex = 0;
        T minOutput = entries[0].output;
        T maxOutput = entries[0].output;
        for (int i = 1; i < entries.Count; i++)
        {
            if (entries[i].output < minOutput)
            {
                minOutput = entries[i].output;
                minIndex = i;
            }
            if (entries[i].output > maxOutput)
            {
                maxOutput = entries[i].output;
                maxIndex = i;
            }
        }

        // Limit checking - we return the limit when an out of bounds "input" is requested
        if (output <= minOutput) return entries[minIndex].input;
        if (output >= maxOutput) return entries[maxIndex].input;

        // Find the interval that this output falls into
        for (int i = 0; i < entries.Count - 1; i++)
        {
            T o1 = entries[i].output;
            T o2 = entries[i + 1].output;

            // Check if output lies between o1 and o2
            bool between = (output >= o1 && output <= o2) || (output <= o1 && output >= o2);
            if (between)
            {
                // Interpolation factor
                T t = (output - o1) / (o2 - o1);

                // Interpolate input
                T i1 = entries[i].input;
                T i2 = entries[i + 1].input;
                return i1 + t * (i2 - i1);
            }
        }

        // Should never reach here if entries are well-formed
        throw new Exception("Unexpected error in GetInput.");
    }

}
