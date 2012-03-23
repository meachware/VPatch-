﻿//---------------------------------------------------------------------------
//                           -=* VPatch *=-
//---------------------------------------------------------------------------
// Copyright (C) 2001-2005 Koen van de Sande / Van de Sande Productions
//---------------------------------------------------------------------------
// Website: http://www.tibed.net/vpatch
//
// This software is provided 'as-is', without any express or implied
// warranty.  In no event will the authors be held liable for any damages
// arising from the use of this software.
//
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must not
//    claim that you wrote the original software. If you use this software
//    in a product, an acknowledgment in the product documentation would be
//    appreciated but is not required.
// 2. Altered source versions must be plainly marked as such, and must not be
//    misrepresented as being the original software.
// 3. This notice may not be removed or altered from any source distribution.
//---------------------------------------------------------------------------
// Ported to C# 2012 Joshua Cearley
using System;
using System.Collections.Generic;
using System.IO;

namespace VPatch
{
	public sealed class PatchGenerator
	{
		#region Operating Streams
		Stream mSource;
		int mSourceSize;
		Stream mTarget;
		int mTargetSize;
		Stream mPatch;
		#endregion
		
		int mBlockSize;
		
		byte[] mTargetCData;
		int mTargetCDataBaseOffset;
		int mTargetCDataSize;
		
		#region Constants
		public const int TargetBufferSize = 65536;
		public const int TargetLookaheadSize = 4096;
		public const int DefaultBlockSize = 64;
		public const int MaxBlockSize = 16384;
		public const int DefaultMaxMatches = 500;
		#endregion
		
		public PatchGenerator(Stream source, int sourceSize,
		                        Stream target, int targetSize,
		                       Stream patch)
		{
			if (source == null)
				throw new ArgumentNullException();
			if (target == null)
				throw new ArgumentNullException();
			if (patch == null)
				throw new ArgumentNullException();
			
			mSource = source;
			mSourceSize = sourceSize;
			
			mTarget = target;
			mTargetSize = targetSize;
			
			mPatch = patch;
			
			mTargetCData = new byte[TargetBufferSize];
			
			Verbose = false;
			MaximumMatches = DefaultMaxMatches;
			mBlockSize = DefaultBlockSize;
		}
		
		/// <param name="sameBlocks">
		/// This list will store blocks that have been found to have remained
		/// the same between files.
		/// </param>
		public void Execute(IList<SameBlock> sameBlocks)
		{
			if (sameBlocks == null)
				throw new ArgumentNullException();
			
			ChunkedFile sourceTree = new ChunkedFile(mSource, mSourceSize, mBlockSize);
			
			// the vector needs an 'empty' first block so checking for overlap with the 'previous' block never fails.
			sameBlocks.Add(new SameBlock());
			
			mTargetCDataBaseOffset = 0;
			mTargetCDataSize = 0;
			bool firstRun = true;
			
			// currentOffset is in the target file
			for (int currentOffset = 0; currentOffset < mTargetSize;) {
				bool reloadTargetCData = true;
				
				if ((currentOffset >= mTargetCDataBaseOffset) &&
				    ((currentOffset + TargetLookaheadSize) < (mTargetCDataBaseOffset + TargetBufferSize)))
				{
					if (firstRun) {
						firstRun = false;
					} else {
						reloadTargetCData = true;
					}
				}
				
				if (reloadTargetCData) {
					// at least support looking back blockSize, if possible (findBlock relies on this!)
					mTargetCDataBaseOffset = currentOffset - mBlockSize;
					// handle start of file correctly
					mTargetCDataSize = TargetBufferSize;
					
					// check if this does not extend beyond EOF
					if ((mTargetCDataBaseOffset + mTargetCDataSize) > mTargetSize) {
						mTargetCDataSize = mTargetSize - mTargetCDataBaseOffset;
					}
					
					// we need to update the memory cache of target
					// TODO: Emit debug info here, if verbose is enabled.
					// cout << "[CacheReload] File position = " << static_cast<unsigned int>(targetCDataBaseOffset) << "\n";
					
					mTarget.Seek(mTargetCDataBaseOffset, SeekOrigin.Begin);
					mTarget.Read(mTargetCData, 0, mTargetCDataSize);
				}
				
				SameBlock currentSameBlock = FindBlock(sourceTree, currentOffset);
				if (currentSameBlock != null) {
					// We have a match.
					SameBlock previousBlock = sameBlocks[sameBlocks.Count-1];
					if ((previousBlock.TargetOffset + previousBlock.Size) > currentSameBlock.TargetOffset) {
						// There is overlap, resolve it.
						int difference = previousBlock.TargetOffset + previousBlock.Size - currentSameBlock.TargetOffset;
						currentSameBlock.SourceOffset += difference;
						currentSameBlock.TargetOffset += difference;
						currentSameBlock.Size -= difference;
					}
					sameBlocks.Add(currentSameBlock);
					
					// TODO: Emit debug info here, if verbose is enabled.
					
					currentOffset = currentSameBlock.TargetOffset + currentSameBlock.Size;
				} else {
					// No match, advance to the next byte.
					currentOffset++;
				}
			}
			
			// Add a block at the end to prevent bounds checking hassles.
			SameBlock lastBlock = new SameBlock();
			lastBlock.TargetOffset = mTargetSize;
			sameBlocks.Add(lastBlock);
		}
		
		SameBlock FindBlock(ChunkedFile sourceTree, int targetFileStartOffset)
		{
			if ((mTargetSize - targetFileStartOffset) < BlockSize) return null;
			
			int preDataSize = targetFileStartOffset - mTargetCDataBaseOffset;
			// rea the current data part in to memory
			ChunkChecksum checksum = new ChunkChecksum();
			sourceTree.CalculateChecksum(mTargetCData, preDataSize, BlockSize, ref checksum);
			
			int foundIndex;
			if (sourceTree.Search(checksum, out foundIndex)) {
				// we found something
				SameBlock bestMatch = new SameBlock();
				bestMatch.SourceOffset = sourceTree.Chunks[foundIndex].Offset;
				bestMatch.TargetOffset = targetFileStartOffset;
				bestMatch.Size = 0; // default to 0. because they can all be mismatches as well
				
				// inreae match size if possible, also check if it is a match at all
				int matchCount = 0;
				while ((sourceTree.Chunks[foundIndex].Checksum == checksum) &&
				       ((MaximumMatches == 0) || (matchCount < MaximumMatches)))
				{
					// check if this one is better than the current match
					SameBlock match = new SameBlock();
					match.SourceOffset = sourceTree.Chunks[foundIndex].Offset;
					match.TargetOffset = targetFileStartOffset;
					match.Size = 0; // default to 0. could be a mismatch with the same key
					ImproveSameBlockMatch(ref match, bestMatch.Size);
					if (match.Size > bestMatch.Size) {
						bestMatch = match;
					}
					foundIndex++;
					matchCount++;
				}
				
				// TODO: Emit debugging information here if in verbose mode.
				
				if (bestMatch.Size == 0) {
					return null;
				} else {
					return bestMatch;
				}
			} else {
				return null;
			}
		}
		
		public const int ComparisonSize = 2048;
		
		void ImproveSameBlockMatch(ref SameBlock match, int currentBest)
		{
			// we should now try to make the match longer by reading big chunks of the files to come
			mSource.Seek(match.SourceOffset + match.Size, SeekOrigin.Begin);
			mTarget.Seek(match.TargetOffset + match.Size, SeekOrigin.Begin);
			
			{
				byte[] sourceData = new byte[ComparisonSize];
				byte[] targetData = new byte[ComparisonSize];
				while (true) {
					int startTarget = match.TargetOffset + match.Size;
					int startSource = match.SourceOffset + match.Size;
					int checkSize = ComparisonSize;
					
					if (checkSize > (mTargetSize - startTarget)) {
						checkSize = mTargetSize - startTarget;
					}
					
					if (checkSize > (mSourceSize - startSource)) {
						checkSize = mSourceSize - startSource;
					}
					
					mSource.Read(sourceData, 0, checkSize);
					mTarget.Read(targetData,0, checkSize);
					
					// TODO: Could we optimize this with either an array primitive or unsafe pointers?
					
					int i = 0;
					while ((sourceData[i] == targetData[i]) &&
					       (i < checkSize))
					{
						match.Size++;
						i++;
					}
					
					// check if we stopped because we had a mismatch
					if (i < checkSize) break;
				}
			}
			
			if (match.Size < BlockSize) {
				match.Size = 0;
			} else {
				// try to improve before match if this is useful
				if ((match.Size + BlockSize) <= currentBest) return;
				// do not do if there is no more data in the target...
				if (match.TargetOffset == 0) return;
				
				// we know it is stored in the cache... so we just need the source one
				byte[] sourceData = new byte[MaxBlockSize];
				
				int startSource = match.SourceOffset - BlockSize;
				int checkSize = BlockSize;
				
				if (checkSize > match.SourceOffset) {
					checkSize = match.SourceOffset;
					startSource = 0;
				}
				
				if (checkSize == 0) return;
				
				mSource.Seek(startSource, SeekOrigin.Begin);
				mSource.Read(sourceData, 0, checkSize);
				checkSize--;
				
				while (sourceData[checkSize] == (mTargetCData[match.TargetOffset - mTargetCDataBaseOffset - 1])) {
					match.TargetOffset--;
					match.SourceOffset--;
					match.Size++;
					checkSize--;
					if (checkSize == 0) break;
					if (match.TargetOffset == 0) break;
				}
			}
		}
		
		#region Public Properties
		public int BlockSize
		{
			get {
				return mBlockSize;
			}
			
			set {
				if (value > MaxBlockSize)
					throw new InvalidOperationException("Given block size exceeds maximum allowed value.");
				if ((value % 2) != 0)
					throw new InvalidOperationException("BlockSize must be a multiple of two!");
				mBlockSize = value;
			}
		}
		
		public int MaximumMatches { get; set; }
		public bool Verbose { get; set; }
		#endregion
	}
}