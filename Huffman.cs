// HUFFMAN CODING (lossless data compression)
// 
// HuffmanEncoder - Encodes input text file byte by byte into an output file 
//                - Output file has 3 parts
//                - First part is a header consisted of 8 values : 0x7B 0x68 0x75 0x7C 0x6D 0x7D 0x66 0x66
//                - Second part is "Huffman coding tree" where byte (in LittleEndian format) :
//                  bit 0 : 1 for IsLeaf node, 0 for inner node
//                  bits 1-55 : lower 55 bits of "weight" / "count of occurence" of the node
//                  bits 56-63 : value of character for IsLeaf node, 0 for inner node
//                - Third part is encoded text of input file as :
//                  Every character in the original file is encoded into the encoded file as a sequence of bits, which 
//                  is corresponding with a path from the root node of huffman tree to a IsLeaf node with the same 
//                  character. 
//                  A path from node into a left child is encoded by 0, a path from parent into a right child is 
//                  encoded by 1.
//                - The data is encoded as bit stream and paths are in the same sequence as were characters of paths in
//                  the original file.
//                - Because of data can be read/written only as Bytes so coding works -> 0th bit will be stored in 0th 
//                  position of first byte ... to 7th bit same. 8th bit will be stored in 0th position of second byte. 
//                  Last byte has to be completed by 0 if there is not enough bits of paths.
//                  (For example text of BDAACB and BDAACBAA will be coded same way)
//  
// HuffmanDecoder - Checks the header 
//                - Constructs Huffman coding tree 
//                - Decodes text of input file into output file
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Huffman
{
    class Huffman
    {
        static void Main(string[] args)
        {
            if (!BitConverter.IsLittleEndian)
                Error.Message("System", "Actually it's made just for Little Endian -> Little Endian so far");

            if (args.Length < 1)
                Error.Message("Argument", "Missing arguments");

            if (args[0] == "--encode")
            {
                if (args.Length < 2)
                    Error.Message("Argument", "Decode missing input file");

                if (args.Length > 3)
                    Error.Message("Argument", "Decode too many arguments");
                
                if (args.Length == 2)
                    HuffmanEncoder.Encode(args[1], args[1] + ".huff");
                else // args.Length == 3
                    HuffmanEncoder.Encode(args[1], args[2]);
            }
            else if (args[0] == "--decode")
            {
                if (args.Length < 2)
                    Error.Message("Argument", "Decode missing input file");

                if (args.Length > 3)
                    Error.Message("Argument", "Decode too many arguments");

                if (args.Length == 2)
                    HuffmanDecoder.Decode(args[1], args[1] + ".txt");
                else // args.Length == 3
                    HuffmanDecoder.Decode(args[1], args[2]);
            }
            else if (args[0] == "--help")
            {
                Console.WriteLine(" --encode [source]          => encodes source into new file source.huff");
                Console.WriteLine(" --encode [source] [target] => encodes source into new file target");
                Console.WriteLine(" --decode [source]          => decodes source into new file source.txt");
                Console.WriteLine(" --decode [source] [target] => decodes source into new file target");
                Console.WriteLine(" --help                     => returns help");
            }
            else
                Error.Message("Argument", "Unknown action, use --help for list of available actions");
        }
    }

    static class Error
    {
        public static void Message(string type, string message)
        {
            Console.WriteLine(type + " Error - " + message);
            System.Environment.Exit(0);
        }
    }


    /// <summary>
    /// Represents node of binary tree.
    /// </summary>
    class HuffmanNode
    {
        public bool IsLeaf;

        /// <summary> 
        /// if ( IsLeaf) then its character is written here
        /// if (!IsLeaf) then value is not important
        /// </summary>
        public byte Char;

        /// <summary>
        /// if ( IsLeaf) then it's count of occurences of its character is written here
        /// if (!IsLeaf) then it's sum of counts of its childs is written here
        /// </summary>
        public long Count;      
                                
        public HuffmanNode Right;      
                                
        public HuffmanNode Left;

        /// <summary>
        /// Constructor of leaf node.
        /// </summary>
        /// <param name="ch"> character </param>
        /// <param name="c"> count of occurences </param>
        public HuffmanNode(byte ch, long c)
        {
            IsLeaf = true;
            Char = ch;
            Count = c;
            Right = null;
            Left = null;
        }

        /// <summary>
        /// Constructor of inner node.
        /// </summary>
        /// <param name="l"> left node </param>
        /// <param name="r"> right node </param>
        public HuffmanNode(HuffmanNode l, HuffmanNode r)
        {
            IsLeaf = false;
            Count = r.Count + l.Count;
            Right = r;
            Left = l;
        }
    }

    class HuffmanNodeComparer : IComparer<HuffmanNode>
    {
        readonly private bool _charCompare;
        readonly private bool _countCompare;

        /// <summary>
        /// If both parameters are true then compares primary due to count, secondary due to character.
        /// If both parameters are false then always returns 0.
        /// </summary>
        /// <param name="cCompare"> compare counts </param>
        /// <param name="charCompare"> compare characters </param>
        public HuffmanNodeComparer(bool cCompare, bool charCompare)
        {
            _countCompare = cCompare;
            _charCompare = charCompare;
        }

        public int Compare(HuffmanNode x, HuffmanNode y)
        {
            int cComp = x.Count.CompareTo(y.Count);
            int charComp = x.Char.CompareTo(y.Char);

            if (_countCompare && _charCompare)
                return (cComp != 0 ? cComp : charComp);
            if (_countCompare)
                return cComp;
            if (_charCompare)
                return charComp;
            return 0;
        }
    }

    
    //
    //___________________________________________HUFFMAN ENCODE________________________________________
    //

    static class HuffmanEncoder
    {
        private const int CountOfCharacters = 256; // ~ Byte ... Program is set for byte characters

        /// <summary>
        /// Construction of leaves.
        /// </summary>
        /// <param name="ifs"> </param>
        /// <returns> all leaves constructed from ifs sorted in queue </returns>
        private static Queue<HuffmanNode> GetLeaves(FileStream ifs)
        {
            // count represents count of occurences for each character
            long[] counts = new long[CountOfCharacters];
            for (int i = 0; i < CountOfCharacters; ++i) 
                counts[i] = 0;

            // obtain count of occurences for each character
            int b;
            while ((b = ifs.ReadByte()) != -1)
                ++counts[b];

            // create leaves
            List<HuffmanNode> leaves = new List<HuffmanNode>();
            for (int i = 0; i < CountOfCharacters; ++i)
                if (counts[i] > 0) 
                    leaves.Add(new HuffmanNode((byte)i, counts[i]));

            // we want to have leaves ordered primary by counts of occurences 
            // and secondary by characters
            leaves.Sort(new HuffmanNodeComparer(true, true));
            
            // changing list to queue
            return new Queue<HuffmanNode>(leaves);
        }

        /// <summary>
        /// Merges forest of trees into one tree. 
        /// </summary>
        /// <param name="orderedLeaves"> trees ordered due to count </param>
        /// <returns> one tree containing all ordered leaves </returns>
        private static HuffmanNode MergeForestIntoNode(Queue<HuffmanNode> orderedLeaves)
        {
            // queue of merged trees
            Queue<HuffmanNode> trees = new Queue<HuffmanNode>();

            if (orderedLeaves.Count == 0) 
                return null;

            // merge 2 trees that have least counts until we will have only one merged tree
            // algorithm : we will have separatelly merged trees and leaves
            //             both will be sorted due to their counts amongst themselves
            //             select 2 with least counts and merge them into one
            while (!(orderedLeaves.Count == 0 && trees.Count == 1))
            {
                // only one leaf and not merged tree yet => it is already one merged tree
                if (orderedLeaves.Count == 1 && trees.Count == 0)
                {
                    trees.Enqueue(orderedLeaves.Dequeue());
                    continue;
                }
                
                // no trees yet made => first 2 leaves have least counts
                if (trees.Count == 0)
                {
                    HuffmanNode l1 = orderedLeaves.Dequeue();
                    HuffmanNode l2 = orderedLeaves.Dequeue();
                    trees.Enqueue(new HuffmanNode(l1, l2));
                    continue;
                }

                // no leaves anymore => first 2 trees have least counts
                if (orderedLeaves.Count == 0)
                {
                    HuffmanNode mt1 = trees.Dequeue();
                    HuffmanNode mt2 = trees.Dequeue();
                    trees.Enqueue(new HuffmanNode(mt1, mt2));
                    continue;
                }

                // we have at least one tree and one leaf => we need to find 2 trees 
                // with least counts (we preferably select leaves)
                {
                    // selected trees
                    HuffmanNode least1; 
                    HuffmanNode least2; 

                    HuffmanNode leaf1 = orderedLeaves.Peek();
                    HuffmanNode tree1 = trees.Peek();
                    
                    if (leaf1.Count < tree1.Count || leaf1.Count == tree1.Count)
                    {
                        least1 = orderedLeaves.Dequeue();
                        if (orderedLeaves.Count != 0)
                        {
                            HuffmanNode leaf2 = orderedLeaves.Peek();
                            if (leaf2.Count < tree1.Count || 
                                leaf2.Count == tree1.Count)
                                least2 = orderedLeaves.Dequeue();
                            else least2 = trees.Dequeue();
                        }
                        else 
                            least2 = trees.Dequeue();
                    }
                    else // (leaf1.Count > tree1.Count)
                    {
                        least1 = trees.Dequeue();
                        if (trees.Count != 0)
                        {
                            HuffmanNode s2 = trees.Peek();
                            if (leaf1.Count < s2.Count || leaf1.Count == s2.Count) least2 = orderedLeaves.Dequeue();
                            else least2 = trees.Dequeue();
                        }
                        else
                            least2 = orderedLeaves.Dequeue();
                    }
                    HuffmanNode ns = new HuffmanNode(least1, least2);
                    trees.Enqueue(ns);
                }
            }

            HuffmanNode mergedForest = trees.Dequeue();
            return mergedForest;
        }

        /// <summary>
        /// Recursive function to make prefix out of tree (for debug purposes).
        /// </summary>
        /// <param name="tree"> </param>
        /// /// <param name="sb"> </param>
        private static void ConstructPrefixOutOfTree(HuffmanNode tree, StringBuilder sb)
        {
            if (tree.IsLeaf) 
                sb.Append("*" + tree.Char.ToString() + ":" + tree.Count.ToString());
            else
            {
                sb.Append(tree.Count.ToString() + " ");
                ConstructPrefixOutOfTree(tree.Left, sb);
                sb.Append(" ");
                ConstructPrefixOutOfTree(tree.Right, sb);
            }
        }

        /// <summary>
        /// Recursive function that constructs binary prefix notation out of Huffman tree 
        /// </summary>
        /// <param name="huffmanTree"></param>
        /// <param name="fs"></param>
        private static void ConstructPrefixOutOfHuffmanTree(HuffmanNode huffmanTree, FileStream fs)
        {
            if (huffmanTree.IsLeaf) 
            {
                ulong ul = 1;
                ulong weight = (ulong)huffmanTree.Count;
                weight = (weight << 9) >> 8;
                ulong character = (ulong)huffmanTree.Char;
                character = character << 56;
                ul = (ul | weight) | character;
                byte[] flushOut = BitConverter.GetBytes(ul);
                fs.Write(flushOut, 0, flushOut.Length);
            }
            else
            {
                ulong ul = 0;
                ulong weight = (ulong)huffmanTree.Count;
                weight = (weight << 9) >> 8;
                ul = (ul | weight);
                byte[] flushOut = BitConverter.GetBytes(ul);
                fs.Write(flushOut, 0, flushOut.Length);
               
                ConstructPrefixOutOfHuffmanTree(huffmanTree.Left, fs);
                ConstructPrefixOutOfHuffmanTree(huffmanTree.Right, fs);
            }
        }

        /// <summary>
        /// Finds the path to the character in the Huffmans tree.
        /// </summary>
        /// <param name="huffmanTree"></param>
        /// <param name="character"> target character to be found </param>
        /// <param name="path"> path to the target character, where true == right / false == left </param>
        /// <returns> true == target character found in tree / false == target character not found in tree </returns>
        private static bool SearchHuffmanTreeForPath(HuffmanNode huffmanTree, ref ulong character, List<bool> path)
        {
            if (huffmanTree.IsLeaf)
            {
                if (huffmanTree.Char == (byte)character)
                    return true;
                return false;
            }

            bool leftChild = SearchHuffmanTreeForPath(huffmanTree.Left, ref character, path);
            if (leftChild)
            {
                path.Add(false);
                return true;
            }

            bool rightChild = SearchHuffmanTreeForPath(huffmanTree.Right, ref character, path);
            if (rightChild)
            {
                path.Add(true);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Constructs path to each character out of Huffman tree.
        /// </summary>
        /// <param name="huffmanTree"></param>
        /// <param name="paths"> variable that holds paths to each character </param>
        private static void GetPathsOfHuffmanTree(HuffmanNode huffmanTree, List<bool>[] paths)
        {
            for (ulong i = 0; i < CountOfCharacters; ++i)
            {
                List<bool> path = new List<bool>();
                bool pathExists = SearchHuffmanTreeForPath(huffmanTree, ref i, path);
                if (pathExists) 
                    paths[i] = path;
            }
        }

        /// <summary>
        /// Writes header, tree (fills it up) , 64b zero, 
        /// </summary>
        /// <param name="huffmanTree"></param>
        /// <param name="paths"></param>
        /// <param name="ifs"></param>
        /// <param name="ofs"></param>
        private static void GetHuffmanCodingForFile(
            HuffmanNode huffmanTree, 
            List<bool>[] paths, 
            FileStream ifs, 
            FileStream ofs)
        {
            // header
            byte[] header = new byte[] { 0x7B, 0x68, 0x75, 0x7C, 0x6D, 0x7D, 0x66, 0x66 };
            ofs.Write(header, 0, header.Length);

            // tree + 64b zero
            ConstructPrefixOutOfHuffmanTree(huffmanTree, ofs);
            byte[] zero = new byte[] { 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };
            ofs.Write(zero, 0, zero.Length);

            // text
            const int sizeOfByte = 8;
            byte flusher = 0;
            int validBitsOfFlusher = 0;
            int character;

            while ((character = ifs.ReadByte()) != -1)
            {   
                foreach ( bool pathBool in paths[character] )
                {
                    if (validBitsOfFlusher == sizeOfByte)
                    {
                        ofs.WriteByte(flusher);
                        flusher = 0;
                        validBitsOfFlusher = 0;
                    }

                    flusher = (byte)(flusher >> 1);
                    if (pathBool) 
                        flusher = (byte)(flusher | 128);
                    ++validBitsOfFlusher;
                }           
            }

            // emptying flusher 
            {
                if (validBitsOfFlusher != 0)
                {
                    int mod = validBitsOfFlusher % 8;

                    while (mod % 8 != 0)
                    {
                        flusher = (byte)(flusher >> 1);
                        mod++;
                    }
                    ofs.WriteByte(flusher);
                }
            }
        }

        /// <summary>
        /// Creates Huffman tree out of input file and saves it in binary prefix notation into output file.
        /// </summary>
        /// <param name="nameOfInputFile"></param>
        /// <param name="nameOfOutputFile"></param>
        public static void Encode(string nameOfInputFile, string nameOfOutputFile)
        {
            if (!File.Exists(nameOfInputFile))
                Error.Message("File", "Input file does not exist");

            try
            {
                // read input first time and construct Huffman tree
                FileStream ifs = new FileStream(nameOfInputFile, FileMode.Open, FileAccess.Read);
                if (ifs.Length == 0)
                    Error.Message("File", "Input file is empty");
                Queue<HuffmanNode> leaves = GetLeaves(ifs);
                ifs.Close();

                HuffmanNode huffmanTree = MergeForestIntoNode(leaves);
                List<bool>[] pathsToLeavesInHuffmanTree = new List<bool>[CountOfCharacters];
                GetPathsOfHuffmanTree(huffmanTree, pathsToLeavesInHuffmanTree);
                foreach (List<bool> path in pathsToLeavesInHuffmanTree)
                    if (path != null)
                        // paths are constructed in reverse order (in recursion)
                        path.Reverse();

                // read input second time and encode text
                ifs = new FileStream(nameOfInputFile, FileMode.Open, FileAccess.Read);
                FileStream ofs = new FileStream(nameOfOutputFile, FileMode.Create, FileAccess.Write);
                GetHuffmanCodingForFile(huffmanTree, pathsToLeavesInHuffmanTree, ifs, ofs);
                ifs.Close();
                ofs.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Probably File Name Error. Check it out");
                throw;
            }
        }


    }

    //
    //___________________________________________HUFFMAN DECODE________________________________________
    //

    static class HuffmanDecoder
    {

        /// <summary>
        /// Checks, if all characters were used due to their Count.
        /// </summary>
        /// <param name="treeNode"></param>
        /// <returns> true if there are all counts in leaves equal to zero </returns>
        private static bool CheckIfEverythingWasUsed(HuffmanNode treeNode)
        {
            if (treeNode.IsLeaf)
            {
                if (treeNode.Count == 0) 
                    return true;
                return false;
            }
            return CheckIfEverythingWasUsed(treeNode.Left) && CheckIfEverythingWasUsed(treeNode.Right);
        }

        /// <summary>
        /// Checks, if count in each node is valid due to construction. 
        /// Count for leaf must be > 0. 
        /// Count for inner node must be == count of left + count of right and recursively left and right must fullfil 
        /// this constraint.
        /// </summary>
        /// <param name="treeNode"> actual node </param>
        /// <returns> true if node fullfils all constraints </returns>
        private static bool CheckCounts(HuffmanNode treeNode)
        {
            if (treeNode.IsLeaf) 
                return (treeNode.Count > 0);

            if (treeNode.Right == null || treeNode.Left == null) 
                return false;

            if (treeNode.Count == treeNode.Left.Count + treeNode.Right.Count)
                return (CheckCounts(treeNode.Right) && CheckCounts(treeNode.Left));
            
            return false;
        }

        /// <summary>
        /// Recursively reconstructs Huffman tree from encoded postfix.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="actualNode"> actually constructed node </param>
        /// <param name="cont"> if we still should look </param>
        /// <returns> root of Huffman tree </returns>
        private static HuffmanNode CreateTreeFromPrefix(List<ulong> prefix, ref int actualNode, ref bool cont)
        {
            if (!cont)
                return null;

            if (prefix.Count <= actualNode)
            {
                cont = false;
                return null;
            }

            ulong leaf = prefix[actualNode] % 2;
            ulong count = (prefix[actualNode] << 8) >> 9;
            ulong character = prefix[actualNode] >> 56;

            HuffmanNode node = new HuffmanNode((byte)character, (long)count);
            node.IsLeaf = leaf == 1;
            ++actualNode;
            if (leaf == 0)
            {
                node.Left  = CreateTreeFromPrefix(prefix, ref actualNode, ref cont);
                node.Right = CreateTreeFromPrefix(prefix, ref actualNode, ref cont);
            }

            return (node);
        }


        /// <summary>
        /// Reconstructs Huffman tree and decodes the text.
        /// </summary>
        /// <param name="nameOfInputFile"></param>
        /// <param name="nameOfOutputFile"></param>
        public static void Decode(string nameOfInputFile, string nameOfOutputFile)
        {
            if (!File.Exists(nameOfInputFile))
                Error.Message("File", "Input file does not exist");

            // set file streams
            FileStream ifs = new FileStream(nameOfInputFile, FileMode.Open, FileAccess.Read);
            FileStream ofs = new FileStream(nameOfOutputFile, FileMode.Create, FileAccess.Write);

            // header check
            byte[] headerAcquired = new byte[8];
            if (ifs.Length > 7)
                for (int i = 0; i < 8; ++i)
                    headerAcquired[i] = (byte) (ifs.ReadByte());
            else
                Error.Message("File", "Header for Huffman is missing");

            byte[] headerOriginal = new byte[] { 0x7B, 0x68, 0x75, 0x7C, 0x6D, 0x7D, 0x66, 0x66 };
            bool acquiredMatchesOriginal = true;
            for (int i = 0; i < 8; ++i)
                if (headerAcquired[i] != headerOriginal[i]) 
                    acquiredMatchesOriginal = false;
            if (!acquiredMatchesOriginal) 
                Error.Message("File", "Header for Huffman does not match");

            // construct huffman tree from prefix notation
            List<ulong> nodeList = new List<ulong>();

            ulong nodeBeingRead = 1;
            byte[] partsOfNodeBeingRead = new byte[8];
            while ((ifs.Position + 8 <= ifs.Length) && (nodeBeingRead != 0))
            {
                ifs.Read(partsOfNodeBeingRead, 0, 8);
                nodeBeingRead = BitConverter.ToUInt64(partsOfNodeBeingRead, 0);
                if (nodeBeingRead != 0)
                    nodeList.Add(nodeBeingRead);
            }

            if (ifs.Position + 8 > ifs.Length && nodeBeingRead != 0) 
                Error.Message("File", "File is not valid : Data being in unknown state");

            bool validTree = true;
            int actualNodeForTreeConstruction = 0;
            HuffmanNode huffmanTree = CreateTreeFromPrefix(nodeList, ref actualNodeForTreeConstruction, ref validTree);

            if (actualNodeForTreeConstruction != nodeList.Count) 
                Error.Message("File", "File is not valid : Prefix tree is not valid (too many nodes");

            if (!validTree) 
                Error.Message("File", "File is not valid : Prefix tree is not valid (missing nodes)");

            if (!CheckCounts(huffmanTree)) 
                Error.Message("File", "File is not valid : Prefix tree is not valid (invalid counts)");

            // decode text
            HuffmanNode actualNode = huffmanTree;
            int character;
            bool onlyLeftSinceRestart = true;
            bool end = false;
            while ((character = ifs.ReadByte()) != -1)
            {
                byte z = (byte)character;
                
                for (int i = 0; i < 8; ++i)
                {
                    if ((z%2) == 1)
                    {
                        if (end)
                            Error.Message("File", "File is not valid : bit 1 during ending sequence");

                        if (actualNode.Right == null)
                            Error.Message("File", "File is not valid : Wrong path during text decoding");
                        actualNode = actualNode.Right;

                        onlyLeftSinceRestart = false;
                    }
                    else // if ((z % 2) == 0)
                    {
                        if (actualNode.Left == null)
                            Error.Message("File", "File is not valid : Wrong path during text decoding");
                        actualNode = actualNode.Left;
                    }

                    if (actualNode.IsLeaf)
                    {
                        if (actualNode.Count > 0)
                        {
                            ofs.WriteByte(actualNode.Char);
                            --actualNode.Count;
                            actualNode = huffmanTree;
                            onlyLeftSinceRestart = true;
                        }
                        else // we have run out of occurences of character - tricky part
                        {
                            // we will check if path is valid "left only"
                            if (! onlyLeftSinceRestart)
                                Error.Message("File", "File is not valid : bit 1 during ending sequence");

                            // we will put ourselves into state end
                            if (!end)
                                end = true;

                            // we will check if it isn't too soon to be end
                            if (ifs.Position + 1 >= ifs.Length)
                                actualNode = huffmanTree;
                            else 
                                Error.Message("File", "File is not valid : Too many uses of a character");
                        }
                    }

                    // advance
                    z = (byte)(z >> 1);
                }
            }

            if (!CheckIfEverythingWasUsed(huffmanTree)) 
                Error.Message("File", "File is not valid : Some unused characters remain");

            ifs.Close();
            ofs.Close();
        }

        
    }

}
