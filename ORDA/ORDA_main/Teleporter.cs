using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class Teleporter
	{
		FlightData flightData = null;

		enum TeleportState { IDLE=0, BUMP, UP, TGT10K, TGT1K, TGT500, DONE };
		bool teleportActive = false;
		TeleportState teleportState = TeleportState.IDLE;
		float teleportTimer = 0;

		public Teleporter (FlightData fd)
		{
			flightData = fd;
		}

		public void beamMeUpScotty ()
		{
			if(teleportState != TeleportState.IDLE)
				return;

			teleportActive = true;
			teleportState = TeleportState.BUMP;
		}

		public void update (float dt)
		{
			Vessel vessel = flightData.vessel;
			Vessel targetVessel = flightData.targetVessel;

			// inactive or no target?
			if (targetVessel == null || teleportActive == false) {
				teleportActive = false;
				teleportState = TeleportState.IDLE;
				return;
			}

			// wait between transitions
			teleportTimer += dt;
			if(teleportTimer > 1) {
				teleportTimer = 0;

				// teleport state machine
				switch(teleportState) {

				case TeleportState.BUMP:
					// need to take off first
					// bump up if currently on the ground
					if(vessel.LandedOrSplashed == true) {
						vessel.SetWorldVelocity(new Vector3(0,0,-50));
					}
					teleportState = TeleportState.UP;
					break;

				case TeleportState.UP:
					// teleport way up to space if we are not yet in orbit
					if(vessel.orbit.altitude < 100000) {
						vessel.GoOnRails();
						vessel.Translate(new Vector3(0,0,-1000000));
						vessel.GoOffRails();
					}
					teleportState = TeleportState.TGT10K;
					break;

				case TeleportState.TGT10K:
				case TeleportState.TGT1K:
				case TeleportState.TGT500:

					// teleport within 10k, 1k, 500 meters of target
					float d = 100;
					switch(teleportState) {
					case TeleportState.TGT10K: d = 10000; break;
					case TeleportState.TGT1K:  d = 1000;  break;
					default:                   d = 500;   break;
					}
					vessel.GoOnRails();
					Vector3 j = Util.nextVector3() * d;
					Vector3 r = targetVessel.orbit.pos - vessel.orbit.pos + j;
					Vector3 t = new Vector3(r.x, r.z, r.y);
					vessel.Translate(t);
					vessel.GoOffRails();

					// match orbital velocity during last teleport
					if(teleportState == TeleportState.TGT500) {
						Vector3 v = new Vector3d(targetVessel.orbit.vel.x, targetVessel.orbit.vel.z, targetVessel.orbit.vel.y);
						vessel.SetWorldVelocity(v);
					}

					// next step
					switch(teleportState) {
					case TeleportState.TGT10K: teleportState = TeleportState.TGT1K;  break;
					case TeleportState.TGT1K:  teleportState = TeleportState.TGT500; break;
					default:                   teleportState = TeleportState.DONE;	 break;
					}
					break;

				case TeleportState.DONE:
				default:
					// done, deactivate
					teleportActive = false;
					teleportState = TeleportState.IDLE;
					break;
				}
			}
		}
	}
}

