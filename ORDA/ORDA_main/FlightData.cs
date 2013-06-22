using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class FlightData
	{
		// our vessel & target (all set by caller)
		public Vessel vessel = null;
        public float woobleFactor = 0;
		public Vessel targetVessel = null;
		public Part vesselPart = null;
		public Part targetPart = null;
		public bool targetChanged = false;

		// vehicle state
		public Vector3 angularVelocity = Vector3.zero;		// ship frame
		public Vector3 orbitUp = Vector3.zero;				// inertial frame
		public Vector3 orbitVelocity = Vector3.zero;		// inertial frame
		public Vector3 orbitNormal = Vector3.zero;			// inertial frame
        public Vector3 planetZUpAngularVelocity = Vector3.zero;
		public Vector3 targetRelPosition = Vector3.zero;	// inertial frame
		public Vector3 targetRelVelocity = Vector3.zero;	// inertial frame
		public Vector3 targetRelPositionShip = Vector3.zero;// ship frame
		public Vector3 targetRelVelocityShip = Vector3.zero;// ship frame
        public Vector3? firstNodeBurnVector = null;
		public float altitudeASL = 0;
		public float altitudeAGL = 0;
		public float verticalSpeed = 0;
		public float horizontalSpeed = 0;
        public string debugValue = string.Empty;

		// vehicle dynamics
		public float mass = 0;
		public Vector3 CoM = Vector3.zero;
		public Vector3 MoI = Vector3.zero;
		public Vector3 availableTorque = Vector3.zero;		// pitch, roll, yaw
		public Vector3 availableForce = Vector3.zero;
		public Vector3 availableAngAcc = Vector3.zero;
		public Vector3 availableLinAcc = Vector3.zero;
        public Vector3 totalThrustVector = Vector3.zero;
        public Vector3 attError = Vector3.zero;
		public float availableEngineThrust = 0;
		public float availableEngineThrustUp = 0;
		public float availableEngineAcc = 0;
		public float availableEngineAccUp = 0;

		public FlightData ()
		{
		}

		public void update ()
		{
			// center of mass
			CoM = vessel.findWorldCenterOfMass ();

			// get vehicle state
			angularVelocity = vessel.GetTransform().InverseTransformDirection (vessel.rigidbody.angularVelocity);
			orbitUp = Util.reorder (vessel.orbit.pos, 132).normalized;
			orbitVelocity = Util.reorder (vessel.orbit.vel, 132).normalized;
			orbitNormal = -Vector3.Cross (orbitUp, orbitVelocity).normalized;
            planetZUpAngularVelocity = Util.reorder(vessel.mainBody.zUpAngularVelocity, 132);
			if (targetVessel != null) {
				targetRelPosition = Util.reorder (targetVessel.orbit.pos - vessel.orbit.pos, 132);
                targetRelVelocity = Util.reorder (vessel.orbit.vel - targetVessel.orbit.vel, 132);
                targetRelPositionShip = vessel.GetTransform().InverseTransformDirection(targetRelPosition);
                targetRelVelocityShip = vessel.GetTransform().InverseTransformDirection(targetRelVelocity);
			} else {
				targetRelPosition = Vector3.zero;
				targetRelVelocity = Vector3.zero;
				targetRelPositionShip = Vector3.zero;
				targetRelVelocityShip = Vector3.zero;
			}
			altitudeASL = (float)vessel.altitude;
			altitudeAGL = (float)(vessel.altitude - vessel.terrainAltitude);

            if (vessel.patchedConicSolver.maneuverNodes.Count > 0)
            {
                firstNodeBurnVector = vessel.patchedConicSolver.maneuverNodes[0].GetBurnVector(vessel.orbit);
            }
            else
            {
                firstNodeBurnVector = null;
            }

            totalThrustVector = Vector3.zero;
            float numParts = 0;

			foreach (Part p in vessel.parts) {
				if (p.collider != null) {
					Vector3d bottomPoint = p.collider.ClosestPointOnBounds (vessel.mainBody.position);
					float partBottomAlt = (float)(vessel.mainBody.GetAltitude (bottomPoint) - vessel.terrainAltitude);
					altitudeAGL = Mathf.Max (0, Mathf.Min (altitudeAGL, partBottomAlt));
				}

                if (p.State == PartStates.ACTIVE)
                {
                    if (p is LiquidFuelEngine)
                    {
                        ++numParts;
                        LiquidFuelEngine lfe = (LiquidFuelEngine)p;
                        totalThrustVector = totalThrustVector + lfe.transform.TransformDirection(lfe.thrustVector * lfe.maxThrust);
                    }

                    if (p is SolidRocket)
                    {
                        ++numParts;
                        SolidRocket sr = (SolidRocket)p;
                        totalThrustVector = totalThrustVector + sr.transform.TransformDirection(sr.thrustVector * sr.thrust);
                    }
                }
			}

            //debugValue = numParts.ToString();

			Vector3 up = (CoM - vessel.mainBody.position).normalized;
			Vector3 velocityVesselSurface = vessel.orbit.GetVel () - vessel.mainBody.getRFrmVel (CoM);
			verticalSpeed = Vector3.Dot (velocityVesselSurface, up);
			horizontalSpeed = (velocityVesselSurface - (up * verticalSpeed)).magnitude;

			// inspect vessel's parts
			// accumulate mass, inertia, torque and force
			mass = 0;
			MoI = vessel.findLocalMOI (CoM);
			availableTorque = Vector3.zero;
			availableForce = Vector3.zero;
			Vector3 availableForcePos = Vector3.zero;
			Vector3 availableForceNeg = Vector3.zero;
			availableEngineThrust = 0;
			availableEngineThrustUp = 0;

			foreach (Part p in vessel.parts) {
				mass += p.mass;
				MoI += p.Rigidbody.inertiaTensor;

				foreach(PartModule pm in p.Modules) {
					if(pm is ORDA_control_pm) {
						float pyr = ((ORDA_control_pm)pm).torqueFactor;
						availableTorque += new Vector3 (pyr, pyr, pyr);
					}
				}

				// stock command pod
				if (p is CommandPod) {
					float pyr = ((CommandPod)p).rotPower;
					availableTorque += new Vector3 (pyr, pyr, pyr);
				}
				// ORDA command pod
				else if (p is ORDA_control) {
					float pyr = ((ORDA_control)p).torqueFactor;
					availableTorque += new Vector3 (pyr, pyr, pyr);
				}
				// reaction control jets
				else if (p is RCSModule) {
					for (int i=0; i<6; i++) {
						RCSModule rm = (RCSModule)p;
						Vector3 tv = rm.thrustVectors [i];
						if (tv != Vector3.zero) {
							Vector3 tv_world = rm.transform.TransformDirection (rm.thrustVectors [i]).normalized;
                            Vector3 tv_ship = vessel.GetTransform().InverseTransformDirection(tv_world).normalized;
							Vector3 tp_world = rm.transform.position - CoM;
							float thrust = rm.thrusterPowers [i];

							// calculate how much torque this thruster might produce
							float CoM_TV_angle = Mathf.Acos (Vector3.Dot (tv_world, tp_world.normalized));
							float torqueFraction = Mathf.Sin (CoM_TV_angle);
							float torque = thrust * tp_world.magnitude * torqueFraction;

							// TODO figure out how to split into yaw, pitch & roll
							//      assume only 50% to compensate for now
							if (FlightInputHandler.RCSLock == false) {
								availableTorque += (new Vector3 (torque, torque, torque)) * 0.5f;
							}

							// get components in ship frame
							float fx = thrust * Vector3.Dot (tv_ship, new Vector3 (1, 0, 0));
							float fy = thrust * Vector3.Dot (tv_ship, new Vector3 (0, 1, 0));
							float fz = thrust * Vector3.Dot (tv_ship, new Vector3 (0, 0, 1));

							if (fx < 0)
								availableForceNeg.x -= fx;
							else
								availableForcePos.x += fx;
							if (fy < 0)
								availableForceNeg.y -= fy;
							else
								availableForcePos.y += fy;
							if (fz < 0)
								availableForceNeg.z -= fz;
							else
								availableForcePos.z += fz;
						}
					}
				}
				// liquid fuel engine
				if (p is LiquidFuelEngine && p.State == PartStates.ACTIVE) {
					LiquidFuelEngine lfe = (LiquidFuelEngine)p;
                    Vector3 tv = vessel.GetTransform().TransformDirection(lfe.thrustVector);
					float dot = Vector3.Dot(up.normalized, tv.normalized);

					availableEngineThrust += lfe.maxThrust;
					availableEngineThrustUp += lfe.maxThrust * dot;
				}
			}
			availableForce.x = Mathf.Min (availableForceNeg.x, availableForcePos.x);
			availableForce.y = Mathf.Min (availableForceNeg.y, availableForcePos.y);
			availableForce.z = Mathf.Min (availableForceNeg.z, availableForcePos.z);

			// calculate available angular / linear acceleration based on physical properties
			availableAngAcc = new Vector3 (availableTorque.x / MoI.x,
			                              availableTorque.y / MoI.y,
			                              availableTorque.z / MoI.z);
			availableLinAcc = new Vector3 (availableForce.x / mass,
			                              availableForce.y / mass,
			                              availableForce.z / mass);
			availableEngineAcc = availableEngineThrust / mass;
			availableEngineAccUp = availableEngineThrustUp / mass;

			// ...

		}
	}
}

