using System;
using System.Collections;
using System.Collections.Generic;

namespace RaJsonDiffPatch
{
    /// <summary>
    /// Computes the Longest Common Subsequence (LCS) of two arrays using a custom match function.
    /// </summary>
    class ArrayLcs
    {
        private readonly Func<object, object, bool> _matchObject;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayLcs"/> class with the specified object match function.
        /// </summary>
        /// <param name="matchObject">A function that determines whether two objects match.</param>
        public ArrayLcs(Func<object, object, bool> matchObject)
        {
            _matchObject = matchObject;
        }

        /// <summary>
        /// Holds the result of an LCS backtracking operation, including the common subsequence and matching indices.
        /// </summary>
        public class BackTrackResult
        {
            public List<object> sequence { get; set; } = new List<object>();
            public List<int> indices1 { get; set; } = new List<int>();
            public List<int> indices2 { get; set; } = new List<int>();
        }


        /// <summary>
        /// Computes the LCS length matrix for the two arrays.
        /// </summary>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <param name="context">An optional context dictionary.</param>
        /// <returns>A 2D matrix of LCS lengths.</returns>
        private int[][] LengthMatrix(object[] array1, object[] array2, IDictionary context)
        {
            var len1 = array1.Length;
            var len2 = array2.Length;

            // initialize empty matrix of len1+1 x len2+1 (int arrays are zero-initialized)
            var matrix = new int[len1 + 1][];
            for (var x = 0; x < matrix.Length; x++)
            {
                matrix[x] = new int[len2 + 1];
            }
            // save sequence lengths for each coordinate
            for (var x = 1; x < len1 + 1; x++)
            {
                for (var y = 1; y < len2 + 1; y++)
                {
                    if (_matchObject(array1[x - 1], array2[y - 1]))
                    {
                        matrix[x][y] = 1 + matrix[x - 1][y - 1];
                    }
                    else
                    {
                        matrix[x][y] = Math.Max(matrix[x - 1][y], matrix[x][y - 1]);
                    }
                }
            }
            return matrix;
        }

        /// <summary>
        /// Backtracks through the LCS length matrix to recover the common subsequence and matching indices.
        /// </summary>
        /// <param name="matrix">The LCS length matrix.</param>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <param name="index1">The current index in the first array.</param>
        /// <param name="index2">The current index in the second array.</param>
        /// <param name="context">An optional context dictionary.</param>
        /// <returns>The backtracking result containing the common subsequence and indices.</returns>
        private BackTrackResult backtrack(int[][] matrix, object[] array1, object[] array2, int index1, int index2, IDictionary context)
        {
            if (index1 == 0 || index2 == 0)
            {
                return new BackTrackResult();
            }

            if (_matchObject(array1[index1 - 1], array2[index2 - 1]))
            {
                var subsequence = backtrack(matrix, array1, array2, index1 - 1, index2 - 1, context);
                subsequence.sequence.Add(array1[index1 - 1]);
                subsequence.indices1.Add(index1 - 1);
                subsequence.indices2.Add(index2 - 1);
                return subsequence;
            }

            if (matrix[index1][index2 - 1] > matrix[index1 - 1][index2])
            {
                return backtrack(matrix, array1, array2, index1, index2 - 1, context);
            }
            else
            {
                return backtrack(matrix, array1, array2, index1 - 1, index2, context);
            }
        }



        /// <summary>
        /// Computes the LCS of two arrays and returns the common subsequence with matching indices.
        /// </summary>
        /// <param name="array1">The first array.</param>
        /// <param name="array2">The second array.</param>
        /// <param name="context">An optional context dictionary.</param>
        /// <returns>The backtracking result containing the common subsequence and indices.</returns>
        public BackTrackResult Get(Object[] array1, object[] array2, IDictionary context)
        {
            context = context ?? new Dictionary<string, object>();
            var matrix = LengthMatrix(array1, array2, context);
            var result = backtrack(matrix, array1, array2, array1.Length, array2.Length, context);
            //if (typeof array1 == = 'string' && typeof array2 == = 'string')
            //{
            //    result.sequence = result.sequence.join('');
            //}
            return result;
        }
    }
}