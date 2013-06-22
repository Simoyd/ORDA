using System;
using UnityEngine; 

namespace ORDA
{
	public class ORDA_control_pm : PartModule
	{
		// settings
		[KSPField]
		public float torqueFactor;

		// ...
		Vessel myOldVessel = null;
		int oldVesselParts = 0;

		public override void OnAwake()
		{
		}

		public override void OnStart(StartState State)
		{
			myOldVessel = this.vessel;
			oldVesselParts = this.vessel.parts.Count;

			part.force_activate();

			if (torqueFactor <= 0)
				torqueFactor = 10;

			print ("ORDA_control_pm cfg-settings: " + torqueFactor.ToString("F3"));
		} 

		public override void OnUpdate()
		{
		} 

		public override void OnFixedUpdate ()
		{
			// did we get seperated from our vessel? or did we loose some parts?
			if (vessel != myOldVessel || vessel.parts.Count != oldVesselParts) {

				// check if the original vessel is still controllable
				bool isControlled = false;
				foreach (Part p in myOldVessel.parts) {
					if (p is CommandPod || p is ORDA_control) {
						isControlled = true;
						break;
					}
					if (p.partInfo.name.StartsWith ("mumech")) {
						isControlled = true;
						break;
					}
					foreach(PartModule pm in p.Modules) {
						if(pm is ORDA_control_pm) {
							isControlled = true;
							break;
						}
					}
					if(isControlled)
						break;
				}

				// nothing on it to control it
				if (!isControlled) {

					// mark as debris
					myOldVessel.orbit.objectType = Orbit.ObjectType.SPACE_DEBRIS;

					// don't focus on dead debris
					if (FlightGlobals.ActiveVessel == myOldVessel) {
						FlightGlobals.SetActiveVessel (vessel);
					}
				}

				// mark new vessel controllable
				vessel.orbit.objectType = Orbit.ObjectType.VESSEL;

				// activate all parts
				if (!part.isConnected) {
					foreach (Part p in vessel.parts) {
						p.isConnected = true;
						if (p.State == PartStates.IDLE) {
							p.force_activate ();
						}
					}
				}

				// adjust name
				vessel.vesselName = myOldVessel.vesselName;
				myOldVessel = vessel;
				oldVesselParts = vessel.parts.Count;
			}

			if (FlightGlobals.ActiveVessel == this.vessel) {
				// apply torque
				float pitch = FlightInputHandler.state.pitch;
				float roll = FlightInputHandler.state.roll;
				float yaw = FlightInputHandler.state.yaw;
				Vector3 torque = new Vector3 (-pitch, -roll, -yaw) * torqueFactor;
				vessel.rigidbody.AddRelativeTorque (torque);
			}
		}

		public override void OnSave(ConfigNode Node)
		{
		}

		public override void OnLoad(ConfigNode Node)
		{
		} 
	}
}

