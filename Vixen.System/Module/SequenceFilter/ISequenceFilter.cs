﻿using System;
using Vixen.Sys;

namespace Vixen.Module.SequenceFilter {
	public interface ISequenceFilter : IHasSetup {
		bool IsDirty { get; }
		TimeSpan TimeSpan { get; set; }
		ChannelNode[] TargetNodes { get; set; }
		void AffectIntent(IIntentSegment intentSegment, TimeSpan filterRelativeStartTime, TimeSpan filterRelativeEndTime);
	}
}