using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_target : Part
	{
		// init
		protected override void onPartLoad ()
		{
			base.onPartLoad();
		}

		// editor + flight
		protected override void onPartAwake ()
		{
			base.onPartAwake();
		}

		protected override void onPartStart ()
		{
			base.onPartStart();
			stackIcon.SetIcon(DefaultIcons.MYSTERY_PART);
		}

		protected override void onPartDestroy ()
		{
			base.onPartDestroy ();
		}

		// flight
		protected override void onFlightStart ()
		{
			base.onFlightStart ();
		}

		protected override void onPartUpdate ()
		{
			base.onPartUpdate ();
		}

		protected override void onPartFixedUpdate ()
		{
			base.onPartFixedUpdate ();
		}
	}
}