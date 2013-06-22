using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class Propulsion
	{
		const float translationFuelRate = 1.0f;

		FlightData flightData = null;
		Bus bus = null;

		public Propulsion (FlightData fd, Bus b)
		{
			flightData = fd;
			bus = b;
		}

		public void update (float dt)
		{
			if (bus.yprDriven) {
				// control moment gyroscopes
				Vector3 torqueSetting = new Vector3 (bus.pitch, bus.roll, bus.yaw);
				if (torqueSetting != Vector3.zero) {
					// apply torque
					Vector3 torque = Vector3.Scale (flightData.availableTorque, torqueSetting);
					//flightData.vessel.rigidbody.AddRelativeTorque (-torque);

					// distribute torque amongst all parts to reduce jitter when docked
					Vector3 inertialTorque = flightData.vessel.transform.TransformDirection(-torque);
					float frac = 1.0f / flightData.vessel.parts.Count;
					foreach(Part p in flightData.vessel.parts) {
						p.Rigidbody.AddTorque(inertialTorque * frac);
					}
				}
			}

			if (bus.xyzDriven) {
				// invisible internal thrusters
				Vector3 forceSetting = new Vector3 (-bus.x, -bus.y, -bus.z);
				if (forceSetting != Vector3.zero) {
					// try to consume rcs fuel
					bool hasFuel = false;
					if (CheatOptions.InfiniteRCS) {
						hasFuel = true;
					} else {
						float amount = translationFuelRate * dt *
							(forceSetting.x + forceSetting.y + forceSetting.z);
						hasFuel = flightData.vessel.rootPart.RequestRCS (amount, 0);
					}

					// apply force if we got fuel
					if (hasFuel) {
						Vector3 force = Vector3.Scale (forceSetting, flightData.availableForce);
						flightData.vessel.rigidbody.AddRelativeForce (force);
					}
				}
			}
		}
	}
}
