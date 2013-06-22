using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_control : Part
	{
		// settings
		[KSPField]
		public float torqueFactor;

		// ...
		Vessel myOldVessel = null;
		int oldVesselParts = 0;

		//
		// init
		//
		protected override void onPartLoad ()
		{
			base.onPartLoad();
		}

		//
		// editor + flight
		//
		protected override void onPartAwake ()
		{
			base.onPartAwake();
		}

		protected override void onPartStart ()
		{
			base.onPartStart ();
			stackIcon.SetIcon (DefaultIcons.COMMAND_POD);
			myOldVessel = this.vessel;
			oldVesselParts = this.vessel.parts.Count;

			if (torqueFactor <= 0)
				torqueFactor = 10;

			print ("ORDA_control cfg-settings: " + torqueFactor.ToString("F3"));
		}

		protected override void onPartDestroy ()
		{
			base.onPartDestroy ();
		}

		//
		// flight
		//
		protected override void onFlightStart ()
		{
			base.onFlightStart ();
		}

		protected override void onPartFixedUpdate ()
		{
			base.onPartFixedUpdate ();

			// did we get seperated from our vessel? or did we loose some parts?
			if (vessel != myOldVessel || vessel.parts.Count != oldVesselParts) {

				// check if the original vessel is still controllable
				bool isControlled = false;
				foreach(Part p in myOldVessel.parts) {
					if(p is CommandPod || p is ORDA_control) {
						isControlled = true;
						break;
					}
					if(p.partInfo.name.StartsWith("mumech")) {
						isControlled = true;
						break;
					}
				}

				// nothing on it to control it
				if(!isControlled) {
					// mark as debris
					myOldVessel.orbit.objectType = Orbit.ObjectType.SPACE_DEBRIS;

					// don't focus on dead debris
					if(FlightGlobals.ActiveVessel == myOldVessel) {
						FlightGlobals.SetActiveVessel(vessel);
					}
				}

				// mark new vessel controllable
				vessel.orbit.objectType = Orbit.ObjectType.VESSEL;

				// activate all parts
				if (!isConnected) {
					foreach(Part p in vessel.parts) {
						p.isConnected = true;
						if (p.State == PartStates.IDLE) {
							p.force_activate ();
						}
					}
				}

				// adjust name
				vessel.vesselName = myOldVessel.vesselName;
				myOldVessel = vessel;
			}
		}

		protected override void onCtrlUpd (FlightCtrlState s)
		{
			Vector3 torque = new Vector3(-s.pitch, -s.roll, -s.yaw) * torqueFactor;
			vessel.rigidbody.AddRelativeTorque(torque);
		}
	}
}
