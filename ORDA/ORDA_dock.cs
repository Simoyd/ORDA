using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_dock : Part
	{
		// settings
		[KSPField]
		public string undockKey;
		[KSPField]
		public string guiKey;
		[KSPField]
		public float dockPosY;				// [m]
		[KSPField]
		public float dockPosMargin;			// [m]
		[KSPField]
		public float dockPosBreakMargin;	// [m]
		[KSPField]
		public float dockAttMargin;			// [°]
		[KSPField]
		public float dockAttBreakMargin;	// [°]

		const int windowId = 1760;
		const int windowWidth = 200;

		const float default_kl = 1000.0f;		// lin spring coef
		const float default_bl = 10.0f;			// lin damp coef
		const float default_kr = 0.1f;			// rot spring coef
		const float default_br = 0.01f;			// rot damp coef
		const float default_krr = 0.1f;			// rot spring coef (roll)
		const float default_brr = 0.01f;		// rot damp coef (roll)
		const float default_exp = 2.0f;

		const float undockSpringLength = 0.25f;
		const float undockSpringCoef = 25.0f;

		const float idleScanTime = 5.0f;
		const float idleScanVesselDistance = 100.0f;
		const float idleScanPartDistance = 10.0f;

		// state
		bool flightStarted = false;
		bool isPacked = false;

		enum DockState { IDLE=0, DOCKED, UNDOCK };
		DockState dockState = DockState.IDLE;

		float dockingPartnerTimer = 0;
		List<Part> dockingPartnerList = new List<Part>();

		// used to calculate rel. vel. & ang. vel.
		Vector3 prevRelDockPos = new Vector3(0,0,0);
		Vector3 prevEuler1 = new Vector3(0,0,0);
		Vector3 prevEuler2 = new Vector3(0,0,0);

		// valid when DOCKED
		Part dockedPart = null;
		Guid dockedVesselId;
		Vessel dockedVessel = null;
		Vector3 dockedDir = new Vector3(0,0,0);
		float dockedDist = 0;

		bool restorePosition = false;
		bool ignorePackUnpack = false;

		// spring coefficients
		static float kl = default_kl;
		static float bl = default_bl;
		static float kr = default_kr;
		static float br = default_br;
		static float krr = default_krr;
		static float brr = default_brr;
		static float exp = default_exp;

		// gui
		static bool guiVisible = false;
		static float guiTimer = 0;
		static Part guiOwner = null;
		static Rect windowPositionAndSize = new Rect();
		static bool windowPositionInvalid = true;
		static bool windowSizeInvalid = true;

		static string kl_string = null;
		static string bl_string = null;
		static string kr_string = null;
		static string br_string = null;
		static string krr_string = null;
		static string brr_string = null;
		static string exp_string = null;
		static bool dontBreak = false;

		//
		// gui
		//
		private void windowGUI (int windowID)
		{
			if (kl_string == null) {
				kl_string = kl.ToString ("F3");
				bl_string = bl.ToString ("F3");
				kr_string = kr.ToString ("F3");
				br_string = br.ToString ("F3");
				krr_string = krr.ToString ("F3");
				brr_string = brr.ToString ("F3");
				exp_string = exp.ToString ("F3");
			}

			GUILayout.BeginVertical ();
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("kl: " + kl.ToString ("F3"));
			kl_string = GUILayout.TextField (kl_string);
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("bl: " + bl.ToString ("F3"));
			bl_string = GUILayout.TextField (bl_string);
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("kr: " + kr.ToString ("F3"));
			kr_string = GUILayout.TextField (kr_string);
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("br: " + br.ToString ("F3"));
			br_string = GUILayout.TextField (br_string);
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("krr: " + krr.ToString ("F3"));
			krr_string = GUILayout.TextField (krr_string);
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("brr: " + brr.ToString ("F3"));
			brr_string = GUILayout.TextField (brr_string);
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal();
			GUILayout.Label ("exp: " + exp.ToString("F3"));
			exp_string = GUILayout.TextField (exp_string);
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Update")) {
				double d = 0;
				if (Double.TryParse (kl_string, out d))
					kl = (float)d;
				if (Double.TryParse (bl_string, out d))
					bl = (float)d;
				if (Double.TryParse (kr_string, out d))
					kr = (float)d;
				if (Double.TryParse (br_string, out d))
					br = (float)d;
				if (Double.TryParse (krr_string, out d))
					krr = (float)d;
				if (Double.TryParse (brr_string, out d))
					brr = (float)d;
				if (Double.TryParse (exp_string, out d))
					exp = (float)d;
			}
			if (GUILayout.Button ("Reset")) {
				kl = default_kl;
				bl = default_bl;
				kr = default_kr;
				br = default_br;
				krr = default_krr;
				brr = default_brr;
				exp = default_exp;

				kl_string = kl.ToString ("F3");
				bl_string = bl.ToString ("F3");
				kr_string = kr.ToString ("F3");
				br_string = br.ToString ("F3");
				krr_string = krr.ToString ("F3");
				brr_string = brr.ToString ("F3");
				exp_string = exp.ToString ("F3");
			}
			GUILayout.EndHorizontal ();

			dontBreak = GUILayout.Toggle(dontBreak, "Don't break");

			GUILayout.EndVertical();

			// dragable window
			GUI.DragWindow();
		}

		private void drawGUI ()
		{
			if(guiOwner != this || !guiVisible) return;

			if (windowPositionInvalid) {
				windowPositionInvalid = false;
				windowPositionAndSize.x = Screen.width - windowWidth - 10;
				windowPositionAndSize.y = Screen.height / 2;
			}
			if(windowSizeInvalid) {
				windowSizeInvalid = false;
				windowPositionAndSize.width = 10;
				windowPositionAndSize.height = 10;
			}
			GUI.skin = HighLogic.Skin;
			windowPositionAndSize = GUILayout.Window (windowId, windowPositionAndSize, windowGUI, "Dock coef", GUILayout.MinWidth (windowWidth));	 
		}

		//
		// init
		//
		protected override void onPartLoad ()
		{
			base.onPartLoad ();
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
			stackIcon.SetIcon (DefaultIcons.MYSTERY_PART);

			if (undockKey.Length != 1)
				undockKey = "u";
			if (guiKey.Length != 1)
				guiKey = "z";
			if (dockPosY <= 0)
				dockPosY = 0.4f;
			if (dockPosMargin <= 0)
				dockPosMargin = 0.1f;
			if (dockPosBreakMargin <= 0)
				dockPosBreakMargin = 0.15f;
			if (dockAttMargin <= 0)
				dockAttMargin = 10;
			if (dockAttBreakMargin <= 0)
				dockAttBreakMargin = 15;

			print ("ORDA_dock cfg-settings: " +
				undockKey + " " +
				guiKey + " " + 
				dockPosY.ToString ("F3") + " " +
				dockPosMargin.ToString ("F3") + " " +
				dockPosBreakMargin.ToString ("F3") + " " +
				dockAttMargin.ToString ("F3") + " " +
				dockAttBreakMargin.ToString ("F3")
			);
		}

		protected override void onPartDestroy ()
		{
			base.onPartDestroy ();
			print ("ORDA_dock.onPartDestroy " + vessel.vesselName);

			if (guiOwner == this) {
				RenderingManager.RemoveFromPostDrawQueue (0, new Callback (drawGUI));
				guiOwner = null;
			}
		}

		public override void onBackup ()
		{
			base.onBackup ();
			if (!flightStarted)
				return;
			print ("ORDA_dock.onBackup " + vessel.vesselName);

			// save dockinfos in custom part data
			ORDAdockdata d = new ORDAdockdata ();
			if (dockState == DockState.DOCKED || restorePosition == true) {
				d.docked = true;
				d.dockedVesselId = dockedVesselId;
				d.dirX = dockedDir.x;
				d.dirY = dockedDir.y;
				d.dirZ = dockedDir.z;
				d.dist = dockedDist;
			} else {
				d.docked = false;
			}

			print ("docked: " + d.docked.ToString());
			customPartData = Convert.ToBase64String (KSP.IO.IOUtils.SerializeToBinary (d)).Replace ("=", "*").Replace ("/", "|");
		}

		//
		// flight
		//
		protected override void onFlightStart ()
		{
			base.onFlightStart ();
			flightStarted = true;

			print ("ORDA_dock.onFlightStart " + vessel.vesselName);

			// try to deserialize customPartData
			ORDAdockdata d = null;
			try {
				d = (ORDAdockdata)KSP.IO.IOUtils.DeserializeFromBinary (Convert.FromBase64String (customPartData.Replace ("*", "=").Replace ("|", "/")));
			} catch (Exception) {
				return;
			}

			print ("deserialized custom part data:");
			print ("docked: " + d.docked.ToString ());
			print ("id: " + d.dockedVesselId.ToString ());
			print ("dir: " + d.dirX.ToString () + " " + d.dirY.ToString () + " " + d.dirZ.ToString ());
			print ("dist: " + d.dist.ToString ());

			if (d.docked) {
				// try to find vessel by id
				Vessel foundVessel = null;
				foreach (Vessel v in FlightGlobals.Vessels) {
					if (v.id == d.dockedVesselId) {
						foundVessel = v;
						break;
					}
				}
				if (foundVessel) {
					// request to restore our position, can't do it here
					dockState = DockState.IDLE;
					restorePosition = true;

					dockedPart = null;
					dockedVesselId = d.dockedVesselId;
					dockedVessel = foundVessel;
					dockedDir = new Vector3 (d.dirX, d.dirY, d.dirZ);
					dockedDist = d.dist;

				} else {
					print ("ORDA_dock.onFlightStart: unable to find docked vessel " + d.dockedVesselId.ToString ());
				}
			}
		}

		protected override void onPartUpdate ()
		{
			base.onPartUpdate ();

			if (guiOwner == null) {
				guiOwner = this;
				RenderingManager.AddToPostDrawQueue (0, new Callback (drawGUI));
			}
			if (guiOwner == this) {
				if (Input.GetKey (guiKey)) {
					guiTimer += Time.deltaTime;
					if (guiTimer > 2) {
						guiVisible = !guiVisible;
						guiTimer = 0;
					}
				} else {
					guiTimer = 0;
				}
			}

			if (!flightStarted)
				return;
			if (FlightGlobals.ActiveVessel != this.vessel) 
				return;
			if(this.packed || isPacked) 
				return;

			// undock?
			if(dockState == DockState.DOCKED) {
				if (Input.GetKeyDown (undockKey)) {
					dockState = DockState.UNDOCK;
					print ("UNDOCK");
				}
			}
		}

		protected override void onPartFixedUpdate ()
		{
			base.onPartFixedUpdate ();

			if (!flightStarted)
				return;

			float dt = Time.fixedDeltaTime;
			float t = Time.fixedTime;

			//
			// ++
			// flightStart @ 2km
			// unpack @ 200m
			// 
			// --
			// backup & destroy @ 2.5km
			//
			//

			// packed? (=no physics simulation for this vessel)
			if (this.packed || isPacked) {

				// maintain relative position while warping
				if(dockState == DockState.DOCKED && dockedVessel != null) {
					vessel.SetPosition (dockedVessel.transform.position + dockedDir * dockedDist);
				}

				// done
				return;
			}

			// docked to unloaded vessel?
			if( (dockState == DockState.DOCKED || dockState == DockState.UNDOCK) &&
			    (dockedPart == null || dockedVessel == null)) {
				return;
			}

			// 
			// restore relative position? 
			// can be requested by
			// - onFlightStart
			// - onUnpack
			// - ourself(see below)
			//
			if (restorePosition) {

				// restore-vessel still packed, try again later
				if(dockedVessel != null) {
					if(dockedVessel.packed) {
						print ("ORDA_dock.onPartFixedUpdate: dockedVessel.packed==true, try again later");
						return;
						// TODO won't work if we drifted away too far
					}
				}

				// acknowledge
				restorePosition = false;

				// oops?
				if (dockedVessel == null) {
					print ("ORDA_dock.onPartFixedUpdate: restoreVessel==null");
					return;
				}

				print ("ORDA_dock.onPartFixedUpdate: restoring positon dir=" + dockedDir.ToString ("F3") + " dist=" + dockedDist.ToString("F3"));

				// restore position
				ignorePackUnpack = true;
				vessel.GoOnRails ();
				vessel.SetPosition (dockedVessel.transform.position + dockedDir * dockedDist);
				vessel.GoOffRails ();
				ignorePackUnpack = false;

				// stop rotation (no effect?)
				vessel.rigidbody.angularVelocity = new Vector3 (0, 0, 0);
				dockedVessel.rigidbody.angularVelocity = new Vector3 (0, 0, 0);

				// match orbital velocity
				vessel.orbit.vel = dockedVessel.orbit.vel;

				// look for a target part to dock
				Part closestPart = null;
				float closest = 0;
				foreach (Part p in dockedVessel.parts) {
					if (ORDA_dock.isDockable (p)) {
						Vector3 dockPos = transform.position + transform.TransformDirection (new Vector3 (0, dockPosY, 0));
						Vector3 relDockPos = p.transform.position - dockPos;
						float d = relDockPos.magnitude;
						if (d < closest || closestPart == null) { // TODO check orientation too
							closest = d;
							closestPart = p;
						}
					}
				}
				print ("closest: " + closest.ToString("F3"));

				// found a usable target part?
				if (closestPart != null) {
					// dock if pretty close
					if(closest < dockPosMargin) {
						dockState = DockState.DOCKED;
						dockedPart = closestPart;
						dockedVessel = dockedPart.vessel;
						dockedVesselId = dockedVessel.id;

						prevEuler1 = new Vector3 (0, 0, 0); // TODO
						prevEuler2 = new Vector3 (0, 0, 0);
						prevRelDockPos = new Vector3 (0, 0, 0);

						print ("ORDA_dock.onPartFixedUpdate: docked to target at " + closest.ToString ("F3"));
					}
					// get relative error and try again
					else {
						Vector3 dockPos = transform.position + transform.TransformDirection (new Vector3 (0, dockPosY, 0));
						Vector3 relDockPos = closestPart.transform.position - dockPos;

						print ("ORDA_dock.onPartFixedUpdate: not close enough to dock, trying again " + relDockPos.ToString("F3"));

						// adjust position and try again
						Vector3 p = dockedDir * dockedDist + relDockPos;
						restorePosition = true;
						dockedDir = p.normalized;
						dockedDist = p.magnitude;
					}
				}
				// duh
				else {
					print ("ORDA_dock.onPartFixedUpdate: unable to find target");
				}

				return;
			}

			// handle docking state machine
			// ready to dock
			if (dockState == DockState.IDLE) {

				// look for docking partners
				dockingPartnerTimer += dt;
				if (dockingPartnerTimer > idleScanTime) {
					dockingPartnerTimer = 0;
					dockingPartnerList.Clear ();

					// might not scale very good :)
					foreach (Vessel v in FlightGlobals.Vessels) {
						if(v == this.vessel) continue;
						float distance = (float)(v.orbit.pos - orbit.pos).magnitude;
						if (distance > idleScanVesselDistance)
							continue;
						foreach (Part p in v.parts) {
							if(ORDA_dock.isDockable(p)) {
								distance = (transform.position - p.transform.position).magnitude;
								if (distance < idleScanPartDistance) {
									dockingPartnerList.Add (p);
								}
							}
						}
					}
				}

				// check potential docking partners
				foreach (Part p in dockingPartnerList) {

					// get relative position & distance
					Vector3 dockPos = transform.position + transform.TransformDirection (new Vector3 (0, dockPosY, 0));
					Vector3 relDockPos = p.transform.position - dockPos;
					float distance = relDockPos.magnitude;

					// get relative attitude & angle
					Quaternion relRotation1 = Quaternion.Inverse (transform.rotation) * p.transform.rotation;
					Quaternion relRotation2 = Quaternion.Inverse (p.transform.rotation) * transform.rotation;
					Vector3 euler1 = relRotation1.eulerAngles;
					Vector3 euler2 = relRotation2.eulerAngles;
					euler1.z += 180;
					euler2.z += 180;
					euler1 = eulerCenter(euler1);
					euler2 = eulerCenter(euler2);
					float pitchErr = Mathf.Abs(euler1.x);
					float rollErr = Mathf.Abs(euler1.y);
					float yawErr = Mathf.Abs(euler1.z);

					// docking conditions met?
					if (distance < dockPosMargin &&
					    pitchErr < dockAttMargin &&
					   	rollErr < dockAttMargin &&
					    yawErr < dockAttMargin) {

						// enter docked state
						dockState = DockState.DOCKED;
						dockedPart = p;
						dockedVessel = p.vessel;
						dockedVesselId = dockedVessel.id;
						print ("DOCKED");

						prevRelDockPos = relDockPos;
						prevEuler1 = euler1;
						prevEuler2 = euler2;
						break;
					}
				}
			}
			// docked to a part from another vessel
			else if (dockState == DockState.DOCKED) {

				// force,torque = (1+e)^exp*k - (de/dt)*b

				// position
				Vector3 dockPos = transform.position + transform.TransformDirection (new Vector3 (0, dockPosY, 0));
				Vector3 relDockPos = dockedPart.transform.position - dockPos;
				Vector3 vel = (prevRelDockPos - relDockPos) / dt;
				prevRelDockPos = relDockPos;
				Vector3 relDockPosSquare = squareVector(relDockPos);
				Vector3 force = relDockPosSquare * kl - vel * bl;

				// attitude
				Quaternion relRotation1 = Quaternion.Inverse (transform.rotation) * dockedPart.transform.rotation;
				Vector3 euler1 = relRotation1.eulerAngles;
				euler1.z += 180;
				euler1 = eulerCenter(euler1);
				Vector3 eulerVel1 = (prevEuler1 - euler1) / dt;
				prevEuler1 = euler1;
				Vector3 eulerSquare1 = squareVector(euler1);
				Vector3 torque1 = eulerSquare1 * kr - eulerVel1 * br;
				torque1.y = eulerSquare1.y * krr - eulerVel1.y * brr;

				Quaternion relRotation2 = Quaternion.Inverse (dockedPart.transform.rotation) * transform.rotation;
				Vector3 euler2 = relRotation2.eulerAngles;
				euler2.z += 180;
				euler2 = eulerCenter (euler2);
				Vector3 eulerVel2 = (prevEuler2 - euler2) / dt;
				prevEuler2 = euler2;
				Vector3 eulerSquare2 = squareVector(euler2);
				Vector3 torque2 = eulerSquare2 * kr - eulerVel2 * br;
				torque2.y = eulerSquare2.y * krr - eulerVel2.y * brr;

				// break connection?
				if((Mathf.Abs (relDockPos.x) > dockPosBreakMargin ||
				    Mathf.Abs (relDockPos.y) > dockPosBreakMargin ||
				    Mathf.Abs (relDockPos.z) > dockPosBreakMargin ||
				    Mathf.Abs (euler1.x) > dockAttBreakMargin ||
				    Mathf.Abs (euler1.y) > dockAttBreakMargin ||
				    Mathf.Abs (euler1.z) > dockAttBreakMargin) && !dontBreak) {
					print ("UNDOCK (overstressed) " + relDockPos.ToString ("F3") + " " + euler1.ToString("F3"));
					dockState = DockState.UNDOCK;
				}
				// apply torque (pry) & force
				else {

					// get physical properties (TODO dont do this on every fixedupdate?)
					float m1 = 0, m2 = 0;
					Vector3 MoI1 = vessel.findLocalMOI (vessel.findWorldCenterOfMass ());
					Vector3 MoI2 = dockedVessel.findLocalMOI (dockedVessel.findWorldCenterOfMass ());
					foreach (Part p in vessel.parts) {
						m1 += p.mass;
						MoI1 += p.Rigidbody.inertiaTensor;
					}
					foreach (Part p in dockedVessel.parts) {
						m2 += p.mass;
						MoI2 += p.Rigidbody.inertiaTensor;
					}

					// docking ships of different sizes can cause strange effects
					// apply torque based on the ships moment of inertia
					float MoImag1 = MoI1.magnitude;
					float MoImag2 = MoI2.magnitude;
					float minMag = Mathf.Min (MoImag1, MoImag2);

					// apply force to docking adapters
					rigidbody.AddForce (force);
					dockedPart.rigidbody.AddForce (-force);

					// ...
					//rigidbody.AddRelativeTorque (torque1 * MoImag1);
					//dockedPart.rigidbody.AddRelativeTorque (torque2 * MoImag2);

					// the amount of torque we can apply to one part seems to be very limited
					// so we distribute it amongst all parts instead
					Vector3 inertialTorque1 = transform.TransformDirection(torque1 * minMag);
					float frac1 = 1.0f / (float)vessel.parts.Count;
					Vector3 inertialTorque2 = dockedPart.transform.TransformDirection(torque2 * minMag);
					float frac2 = 1.0f / (float)dockedVessel.parts.Count;

					foreach(Part p in vessel.parts) {
						p.Rigidbody.AddTorque(inertialTorque1 * frac1);
					}
					foreach(Part p in dockedVessel.parts) {
						p.Rigidbody.AddTorque(inertialTorque2 * frac2);
					}
				}
			}
			// undock request
			else if (dockState == DockState.UNDOCK) {

				// get relative position & distance
				Vector3 dockPos = transform.position + transform.TransformDirection (new Vector3 (0, dockPosY, 0));
				Vector3 relDockPos = dockedPart.transform.position - dockPos;
				float distance = relDockPos.magnitude;

				// push parts apart?
				if(distance < undockSpringLength) {

					// apply forces
					float force = undockSpringCoef * (undockSpringLength - distance);
					rigidbody.AddRelativeForce (new Vector3 (0, -force, 0));
					dockedPart.rigidbody.AddRelativeForce (new Vector3 (0, -force, 0));
				}

				// far enough?
				if(distance > dockPosMargin * 4) {

					// back to idle state
					dockState = DockState.IDLE;
					dockedPart = null;
					dockedVessel = null;
					print ("IDLE");
				}
			}

			// keep restore infos up to date
			if (dockState == DockState.DOCKED) {
				// just to be on the safe side
				if(FlightGlobals.ActiveVessel != null) {
					// only when active vessel is nearby to reduce errors
					float viewerDist = (FlightGlobals.ActiveVessel.transform.position - transform.position).magnitude;
					if(viewerDist < 100) {
						Vector3 relPos = vessel.transform.position - dockedPart.vessel.transform.position;
						Vector3 dir = relPos.normalized;
						float dist = relPos.magnitude;

						dockedDir = dir;
						dockedDist = dist;
						dockedVessel = dockedPart.vessel;
					}
				}
			}
		}

		protected override void onPack ()
		{
			base.onPack ();
			if (ignorePackUnpack == false) {
				print ("ORDA_dock.onPack " + vessel.vesselName);
				isPacked = true;
			}
			return;
		}

		protected override void onUnpack ()
		{
			base.onUnpack ();

			if (ignorePackUnpack == false) {
				print ("ORDA_dock.onUnpack " + vessel.vesselName);
				isPacked = false;

				if(dockedVessel != null) {
					restorePosition = true;
				}
			}
			return;
		}

		//
		// utils
		//
		private Vector3 eulerCenter (Vector3 e)
		{
			if (e.x > 180) {
				e.x -= 360;
			} else if (e.x < -180) {
				e.x += 360;
			}

			if (e.y > 180) {
				e.y -= 360;
			} else if (e.y < -180) {
				e.y += 360;
			}

			if (e.z > 180) {
				e.z -= 360;
			} else if (e.z < -180) {
				e.z += 360;
			}
			return e;
		}

		private Vector3 squareVector (Vector3 v)
		{
			Vector3 f = Vector3.zero;
			f.x = Mathf.Pow(1 + Mathf.Abs(v.x), exp);
			f.y = Mathf.Pow(1 + Mathf.Abs(v.y), exp);
			f.z = Mathf.Pow(1 + Mathf.Abs(v.z), exp);

			/*float e = exp;
			Vector3 f = new Vector3 (1+Mathf.Abs(v.x), 1+Mathf.Abs(v.y), 1+Mathf.Abs(v.z));
			f = new Vector3(Mathf.Pow(f.x, e), Mathf.Pow(f.y, e), Mathf.Pow(f.z, e));*/
			return Vector3.Scale(v, f);
		}

		public void getRelPosAndAtt(Part p, out Vector3 relPosOut, out float distanceOut, out Vector3 eulerOut)
		{
			// get relative position & distance
			Vector3 dockPos = transform.position + transform.TransformDirection (new Vector3 (0, dockPosY, 0));
			Vector3 relDockPos = p.transform.position - dockPos;
			float distance = relDockPos.magnitude;

			// get relative attitude & angle
			Quaternion relRotation1 = Quaternion.Inverse (transform.rotation) * p.transform.rotation;
			Vector3 euler1 = relRotation1.eulerAngles;
			euler1.z += 180;
			euler1 = eulerCenter(euler1);

			relPosOut = transform.InverseTransformDirection(relDockPos);
			distanceOut = distance;
			eulerOut = euler1; // pitch roll yaw
		}

		public bool isDocked()
		{
			if(dockState == DockState.DOCKED) {
				return true;
			}
			return false;
		}

		static public bool isDockable (Part p)
		{
			if (p is ORDA_target || p is ORDA_decoupler) {
				return true;
			}
			return false;
		}
	}

	[Serializable()]
	public class ORDAdockdata
	{
		public bool docked;
		public System.Guid dockedVesselId;
		public float dirX;
		public float dirY;
		public float dirZ;
		public float dist;
	}
};
