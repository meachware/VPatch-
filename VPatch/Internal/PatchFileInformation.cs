﻿/*
 * ---------------------------------------------------------------------------
 *                            -=* VPatch *=-
 * ---------------------------------------------------------------------------
 *  Copyright (C) 2001-2005 Koen van de Sande / Van de Sande Productions
 * ---------------------------------------------------------------------------
 *  Website: http://www.tibed.net/vpatch
 * 
 *  This software is provided 'as-is', without any express or implied
 *  warranty.  In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 * 
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 * 
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 * ---------------------------------------------------------------------------
 * Ported to C# 2012 Joshua Cearley
 */
using System;

namespace VPatch.Internal
{
	public class PatchFileInformation
	{
		byte[] mSourceChecksum;
		byte[] mTargetChecksum;
		
		public PatchFileInformation()
		{
		}
		
		public DateTime TargetDateTime { get; set; }
		
		public uint BlockCount { get; set; }
		
		public uint BodySize { get; set; }
		
		public byte[] SourceChecksum
		{
			get {
				return mSourceChecksum;
			}
			
			set {
				if (value != null && value.Length != 16)
					throw new ArgumentException();
				
				mSourceChecksum = value;
			}
		}
		
		public byte[] TargetChecksum
		{
			get {
				return mTargetChecksum;
			}
			
			set {
				if (value != null && value.Length != 16)
					throw new ArgumentException();
				
				mTargetChecksum = value;
			}
		}
	}
}
