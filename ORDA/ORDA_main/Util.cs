using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	[Serializable()]
	public class sVector3
	{
		public sVector3 (Vector3 v)
		{
			x = v.x;
			y = v.y;
			z = v.z;
		}

		public Vector3 toVector3 ()
		{
			return new Vector3(x, y, z);
		}

		public float x, y, z;
	}

	public class VesselComparer : IComparer<Vessel>
	{
	    Vessel vessel;

		public VesselComparer (Vessel v)
		{
			vessel = v;
		}

	    public int Compare (Vessel a, Vessel b)
		{
			double d = (vessel.orbit.pos - a.orbit.pos).magnitude - (vessel.orbit.pos - b.orbit.pos).magnitude;
			if (d < 0)
				return -1;
			if (d > 0)
				return 1;
			return 0;
		}
	}

	static public class Util
	{
		//
		// sort vessels by distance to own vessel
		//
		static public List<Vessel> getSortedVesselList (Vessel v)
		{
			List<Vessel> vesselList = new List<Vessel>(FlightGlobals.Vessels);
        	vesselList.Sort(new VesselComparer(v));
			return vesselList;
		}

		//
		// vector/scalar utils
		//
		static public Vector3 reorder (Vector3 v, int order)
		{
			Vector3 r = Vector3.zero;

			switch (order) {
			case 123: r = new Vector3(v.x, v.y, v.z); break;
			case 132: r = new Vector3(v.x, v.z, v.y); break;
			case 213: r = new Vector3(v.y, v.x, v.z); break;
			case 231: r = new Vector3(v.y, v.z, v.x); break;
			case 312: r = new Vector3(v.z, v.x, v.y); break;
			case 321: r = new Vector3(v.z, v.y, v.x); break;
			default:  r = new Vector3(v.x, v.y, v.z); break;
			}

			return r;
		}

		static public float maxElement (Vector3 v)
		{
			float absx = Mathf.Abs(v.x);
			float absy = Mathf.Abs(v.y);
			float absz = Mathf.Abs(v.z);
			float max = absx;
			if(absy > max) max = absy;
			if(absz > max) max = absz;
			return max;
		}

		static public string formatValue (double value, string unit, string format="F1")
		{
			string modifier = "";

			double absValue = value;
			if (absValue < 0)
				absValue = -value;

			if (absValue > 1e6) {
				value /= 1e6;
				modifier = "M";
			} else if (absValue > 1e3) {
				value /= 1e3;
				modifier = "k";
			} else if (absValue < 1e0) {
				value *= 1e3;
				modifier = "m";
			} else if (absValue < 1e-3) {
				value *= 1e6;
				modifier = "µ";
			}

			if (modifier.Length > 0) {
				format = "F3";
			}

			string s = value.ToString (format) + modifier + unit;
			return s;
		}

		//
		// orbit utils (not working properly :/)
		//
		static public double trueAnomalyToEccAnomaly(double e, double f)
		{
			double E = Math.Atan( Math.Sqrt((1-e)/(1+e)) * Math.Tan(f/2) );
			if(E < 0.0) E += 3/2 * Math.PI;
			E *= 2.0;
			return E;
		}

		static public double calcMeanAnomaly(double e, double E)
		{
			return E - e * Math.Sin(E);	
		}

		static public double getInterceptTime (Vessel targetVessel, Vessel interceptingVessel, bool ApA)
		{
			// calculates time until interceptingVessel reaches targetVessel's ApA/PeA

			double targetNode = targetVessel.orbit.argumentOfPeriapsis;
			if (ApA) {
				if(targetNode > 180) {
					targetNode -= 180;
				} else {
					targetNode += 180;
				}
			}

			double P = interceptingVessel.orbit.period;			// period
			double e = interceptingVessel.orbit.eccentricity;	// eccentricity
			double te = interceptingVessel.orbit.ObT;			// time at epoch
			double AoP = interceptingVessel.orbit.argumentOfPeriapsis;

			double f = ((targetNode - AoP) / 180) * Math.PI;	// true anomaly to target node
			double E = trueAnomalyToEccAnomaly (e, f);			// eccentric anomaly
			double M = calcMeanAnomaly (e, E);					// mean anomaly
			double n = 2 * Math.PI / P;							// mean motion
			double t = M / n;									// time to true anomaly
			double ti = t - te;									// time to target node
			if(ti < 0) ti += P;

			return ti;
		}

		//
		// impact/landing simulation
		//
		static public bool simulateImpact (FlightData flightData,
		                                   out float outMinAltitude,
		                                   out float outTime,
		                                   out float outVelocity)
		{
			float engineAccel = flightData.availableEngineAccUp * FlightInputHandler.state.mainThrottle;

			// state
			float simAltitue = flightData.altitudeAGL;
			float simMinAltitude = simAltitue;
			float simTime = 0;
			float simVelocity = flightData.verticalSpeed;
			float simStep = 0.1f;
			float simMaxTime = 1000;
			bool simAborted = false;

			// simple integrator
			// TODO (dm/dt)
			while (true) {
				double r = flightData.vessel.mainBody.Radius + simAltitue;
				float g = (float)(flightData.vessel.mainBody.gravParameter / (r * r));

				simVelocity += (engineAccel - g) * simStep;
				simAltitue += simVelocity * simStep;
				simTime += simStep;

				if (simAltitue < simMinAltitude) {
					simMinAltitude = simAltitue;
				}

				if (simTime > simMaxTime) {
					simAborted = true;
					break;
				}
				if (simAltitue < 0) {
					break;
				}
			}

			if (simAborted) {
				outMinAltitude = simMinAltitude;
				outTime = 0;
				outVelocity = 0;
				return false;
			} else {
				outMinAltitude = 0;
				outTime = simTime;
				outVelocity = simVelocity;
				return true;
			}
		}

		//
		// random number utils
		//
		static System.Random teleportRandom = new System.Random();
		static public float nextFloat()
		{
		    double val = teleportRandom.NextDouble(); // range 0.0 to 1.0
			return (float)val;
		}

		static public Vector3 nextVector3()
		{
			return new Vector3(nextFloat(), nextFloat(), nextFloat()).normalized;
		}
	}
}

