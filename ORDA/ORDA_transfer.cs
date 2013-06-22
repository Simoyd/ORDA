using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_transfer : Part
	{
		// settings
		[KSPField]
		public string grappleKey;
		[KSPField]
		public float maxGrappleDistance;
		[KSPField]
		public float maxLineLength;
		[KSPField]
		public float maxFuelFlow;
		[KSPField]
		public float maxRCSFlow;

		const int firstWindowId = 1765;

		// member
		LineRenderer fuelLineRenderer = new LineRenderer();
		static int windowIdCounter = firstWindowId;
		int windowId = -1;
		Rect windowPositionAndSize = new Rect();
		bool windowPositionInvalid = true;
		bool windowSizeInvalid = true;
		const int windowWidth = 200;

		enum LineState { IDLE=0, GRAPPLED, CONNECTED };
		LineState lineState = LineState.IDLE;
		Vessel grappledKerbal = null;
		Vessel connectedShip = null;
		Part connectedTank = null;
		float connectedTankCapacity = 0;

		float totalFuelTransferred = 0;
		bool fuelTransferFlag = false;

		static List<Vessel> grappledKerbalsList = new List<Vessel>();

		// gui
		private void windowGUI (int windowID)
		{
			GUIStyle style = new GUIStyle (GUI.skin.button); 
			style.normal.textColor = style.focused.textColor = Color.white;
			style.hover.textColor = style.active.textColor = Color.yellow;
			style.onNormal.textColor = style.onFocused.textColor = style.onHover.textColor = style.onActive.textColor = Color.green;
			style.padding = new RectOffset (4, 4, 4, 4);

			GUIStyle activeStyle = new GUIStyle (GUI.skin.button); 
			activeStyle.normal.textColor = activeStyle.focused.textColor = Color.red;
			activeStyle.hover.textColor = activeStyle.active.textColor = Color.yellow;
			activeStyle.onNormal.textColor = activeStyle.onFocused.textColor = activeStyle.onHover.textColor = activeStyle.onActive.textColor = Color.green;
			activeStyle.padding = new RectOffset (4, 4, 4, 4);

			GUILayout.BeginVertical ();

			if (connectedShip == null || connectedTank == null) {
				fuelTransferFlag = false;
			} else {
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Transfer", (fuelTransferFlag) ? (activeStyle) : (style))) {
					fuelTransferFlag = !fuelTransferFlag;
				}
				if (GUILayout.Button ("Reset", style)) {
					totalFuelTransferred = 0;
				}
				GUILayout.EndHorizontal();

				GUILayout.Label ("Transferred: " + totalFuelTransferred.ToString ());
				GUILayout.Label ("Connected Ship: " + connectedShip.vesselName);
				//GUILayout.Label ("Connected Tank: " + connectedTank.name);
				//GUILayout.Label ("State: " + connectedTank.State.ToString());

				if (connectedTank is FuelTank) {
					GUILayout.Label ("Type: Fuel");
					GUILayout.Label ("Fuel: " + ((FuelTank)connectedTank).fuel.ToString () + " / " + connectedTankCapacity.ToString ());
				} else if (connectedTank is RCSFuelTank) {
					GUILayout.Label ("Type: RCS Fuel");
					GUILayout.Label ("Fuel: " + ((RCSFuelTank)connectedTank).fuel.ToString () + " / " + connectedTankCapacity.ToString ());
				}
				GUILayout.Label ("Mass: " + connectedTank.rigidbody.mass.ToString ());
			}

			GUILayout.EndVertical();

			// dragable window
			GUI.DragWindow();
		}

		private void drawGUI ()
		{
			// allocate window id
			if (windowId < 0) {
				windowId = windowIdCounter++;
			}

			// hide window?
			if(FlightGlobals.ActiveVessel != this.vessel || !isControllable) return;
			if(lineState != LineState.CONNECTED) return;

			// position / size indicated invalid?
			if (windowPositionInvalid) {
				windowPositionInvalid = false;
				windowPositionAndSize.x = Screen.width / 4 - windowWidth / 2;
				windowPositionAndSize.y = 10;
			}
			if(windowSizeInvalid) {
				windowSizeInvalid = false;
				windowPositionAndSize.width = 10;
				windowPositionAndSize.height = 10;
			}

			// show window
			GUI.skin = HighLogic.Skin;
			windowPositionAndSize = GUILayout.Window (windowId, windowPositionAndSize, windowGUI, "Fuel Transfer", GUILayout.MinWidth (windowWidth));	 
		}

		// init
		protected override void onPartLoad ()
		{
			base.onPartLoad ();
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

			if (grappleKey.Length != 1)
				grappleKey = "g";
			if (maxGrappleDistance <= 0)
				maxGrappleDistance = 2.5f;
			if (maxLineLength <= 0)
				maxLineLength = 50;
			if (maxFuelFlow <= 0)
				maxFuelFlow = 10;
			if (maxRCSFlow <= 0)
				maxRCSFlow = 5;

			print ("ORDA_transfer cfg-settings: " +
				grappleKey + " " +
				maxGrappleDistance.ToString ("F3") + " " + 
				maxLineLength.ToString ("F3") + " " +
				maxFuelFlow.ToString ("F3") + " " +
				maxRCSFlow.ToString ("F3")
			);
		}

		protected override void onPartDestroy ()
		{
			base.onPartDestroy ();

			RenderingManager.RemoveFromPostDrawQueue (0, new Callback (drawGUI));

			// free any kerbals
			if (lineState == LineState.GRAPPLED && grappledKerbal != null) {
				grappledKerbalsList.Remove(grappledKerbal);
				lineState = LineState.IDLE;
			}

			lineState = LineState.IDLE;
			grappledKerbal = null;
			connectedShip = null;
			connectedTank = null;
		}

		// flight
		protected override void onFlightStart ()
		{
			base.onFlightStart ();

			RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));

			GameObject obj = new GameObject ("Line");
			fuelLineRenderer = obj.AddComponent< LineRenderer > ();
			fuelLineRenderer.transform.parent = transform;
			fuelLineRenderer.transform.localPosition = Vector3.zero;
			fuelLineRenderer.transform.localEulerAngles = Vector3.zero;
			fuelLineRenderer.useWorldSpace = false;
			fuelLineRenderer.material = new Material (Shader.Find ("Particles/Additive"));
			fuelLineRenderer.SetWidth (0.2f, 0.2f); 
			fuelLineRenderer.SetVertexCount (2);
			fuelLineRenderer.SetPosition (0, Vector3.zero);
			fuelLineRenderer.SetPosition (1, Vector3.zero);
			fuelLineRenderer.SetColors (Color.gray, Color.gray);
		}

		protected override void onPartUpdate ()
		{
			base.onPartUpdate ();

			float dt = Time.deltaTime;
			Vessel activeVessel = FlightGlobals.ActiveVessel;
			bool keyDown = Input.GetKeyDown (grappleKey);
			LineState oldLineState = lineState;

			// check for illegal states (eg. when a kerbal dies)
			if (
				(lineState == LineState.GRAPPLED && grappledKerbal == null) ||
				(lineState == LineState.CONNECTED && (connectedShip == null || connectedTank == null))
			   ) {
				lineState = LineState.IDLE;
				grappledKerbal = null;
				connectedShip = null;
				connectedTank = null;
				connectedTankCapacity = 0;
			}

			// cut fuel line if too long
			if (lineState == LineState.GRAPPLED) {
				if ((this.transform.position - grappledKerbal.transform.position).magnitude > Mathf.Abs (maxLineLength)) {
					lineState = LineState.IDLE;
					grappledKerbal = null;
					grappledKerbalsList.Remove (grappledKerbal);
					print ("Snap!");
				}
			} else if (lineState == LineState.CONNECTED) {
				if ((this.transform.position - connectedTank.transform.position).magnitude > Mathf.Abs (maxLineLength)) {
					lineState = LineState.IDLE;
					connectedShip = null;
					connectedTank = null;
					connectedTankCapacity = 0;
					print ("Snap!");
				}
			}

			// state logic
			if (lineState == LineState.IDLE) {

				// key pressed?
				if (keyDown) {

					// player controlling a kerbal?
					if (activeVessel.isEVA) {

						// in range?
                        float distance = (this.transform.position - activeVessel.GetTransform().position).magnitude;
						if (distance < Mathf.Abs (maxGrappleDistance)) {

							// kerbal not yet grappled?
							if (grappledKerbalsList.Contains (activeVessel) == false) {

								// grapple fuel line to kerbal
								lineState = LineState.GRAPPLED;
								grappledKerbal = activeVessel;
								connectedShip = null;
								connectedTank = null;
								print ("GRAPPLED at " + distance.ToString () + "m");

								// add to list of grappled kerbals
								grappledKerbalsList.Add (grappledKerbal);
							} else {
								print ("Kerbal already grappled");
							}
						}
					}
				}

			} else if (lineState == LineState.GRAPPLED) {

				// player controlling our kerbal?
				if (activeVessel.isEVA && activeVessel == grappledKerbal) {

					// key pressed?
					if (keyDown) {

						// find closest ship (no kerbals)
						Vessel closestVessel = null;
						float closestVesselDistance = 0;
						foreach (Vessel v in FlightGlobals.Vessels) {
							if (v.isEVA)
								continue;

							Vector3 relPos = v.orbit.pos - activeVessel.orbit.pos;
							float distance = relPos.magnitude;

							if (closestVessel == null || distance < closestVesselDistance) {
								closestVessel = v;
								closestVesselDistance = distance;
							}
						}

						if (closestVessel) {

							// at own vessel?
							if (closestVessel == this.vessel) {

                                Vector3 relPos = activeVessel.GetTransform().position - this.transform.position;
								float distance = relPos.magnitude;

								// in range?
								if (distance < Mathf.Abs (maxGrappleDistance)) {
									// remove kerbal from list
									grappledKerbalsList.Remove (grappledKerbal);

									// return the fuel line
									lineState = LineState.IDLE;
									grappledKerbal = null;
									connectedShip = null;
									connectedTank = null;
									print ("IDLE at " + distance.ToString () + "m");
								}
							} else {
								// find closest fuel or rcs tank
								Part closestTank = null;
								float closestTankDistance = 0;

								foreach (Part p in closestVessel.parts) {
									if (p is FuelTank || p is RCSFuelTank) {
                                        Vector3 relPos = activeVessel.GetTransform().position - p.transform.position;
										float distance = relPos.magnitude;

										if (closestTank == null || distance < closestTankDistance) {
											closestTank = p;
											closestTankDistance = distance;
										}
									}
								}

								// in range?
								if (closestTankDistance < Mathf.Abs (maxGrappleDistance) && closestTank != null) {

									// remove kerbal from list
									grappledKerbalsList.Remove (grappledKerbal);

									// connect fuel line to ship
									lineState = LineState.CONNECTED;
									grappledKerbal = null;
									connectedShip = closestVessel;
									connectedTank = closestTank;
									connectedTankCapacity = getPartFuelCapacity(connectedTank);

									print ("CONNECTED at " + closestTankDistance.ToString () +
										"m capacity=" + connectedTankCapacity.ToString ()
									);
								}
							}
						}
					}
				}

			} else if (lineState == LineState.CONNECTED) {

				// key pressed?
				if (keyDown) {

					// player controlling a kerbal?
					if (activeVessel.isEVA) {

						// in range?
                        float distance = (connectedTank.transform.position - activeVessel.GetTransform().position).magnitude;
						if (distance < Mathf.Abs (maxGrappleDistance)) {

							// kerbal not yet grappled?
							if (grappledKerbalsList.Contains (activeVessel) == false) {

								// grapple fuel line to kerbal
								lineState = LineState.GRAPPLED;
								grappledKerbal = activeVessel;
								connectedShip = null;
								connectedTank = null;
								print ("GRAPPLED at " + distance.ToString () + "m");

								// add to list of grappled kerbals
								grappledKerbalsList.Add (grappledKerbal);
							} else {
								print ("Kerbal already grappled");
							}
						}
					}
				}
			}

			// render logic
			if (lineState == LineState.GRAPPLED) {
				// render fuel line
                Vector3 relPos = grappledKerbal.GetTransform().position - this.transform.position;
				Vector3 ps = transform.InverseTransformDirection (relPos);
				fuelLineRenderer.SetPosition (1, ps);

			} else if (lineState == LineState.CONNECTED) {
				// render fuel line
                Vector3 relPos = connectedTank.transform.position - this.transform.position;
				Vector3 ps = transform.InverseTransformDirection (relPos);
				fuelLineRenderer.SetPosition (1, ps);

			} else {
				// nothing
				fuelLineRenderer.SetPosition (1, Vector3.zero);
			}

			// fuel transfer logic
			if (lineState == LineState.CONNECTED) {
				// transfer fuel or rcs?
				if (fuelTransferFlag) {
					if (connectedTank is FuelTank) {
						FuelTank tank = (FuelTank)connectedTank;
						float totalAmount = Mathf.Abs (maxFuelFlow) * dt;

						// enough space in connected tank?
						if (tank.fuel + totalAmount < connectedTankCapacity) {
							// try to get fuel from own ship
							if (RequestFuel (this, totalAmount, getFuelReqId ())) {
								// reset empty tanks
								if (tank.fuel < 0.001f) {
									tank.fuel = totalAmount;
								}
								// regular transfer
								else {
									tank.fuel += totalAmount;
								}
								totalFuelTransferred += totalAmount;
							}
						}
					} else if (connectedTank is RCSFuelTank) {
						RCSFuelTank tank = (RCSFuelTank)connectedTank;
						float totalAmount = Mathf.Abs (maxRCSFlow) * dt;

						// enough space in connected tank?
						if (tank.fuel + totalAmount < connectedTankCapacity) {
							// try to get fuel from own ship
							if (vessel.rootPart.RequestRCS (totalAmount, 0)) {
								// reset empty tanks
								if (tank.fuel < 0.001f) {
									tank.fuel = totalAmount;
								}
								// regular transfer
								else {
									tank.fuel += totalAmount;
								}
								totalFuelTransferred += totalAmount;
							}
						}
					}
				}
			}

			// re-calculate window size if state changed
			if (oldLineState != lineState) {
				windowSizeInvalid = true;
			}
		}

		protected override void onPartFixedUpdate ()
		{
			base.onPartFixedUpdate();
		}

		// utils
		private float getPartFuelCapacity (Part part)
		{
            /*
			foreach (AvailablePart ap in PartLoader.fetch.loadedPartsList) {
	            if (ap.name == part.partInfo.name) {
					Part p = ap.partPrefab;
					if(p is FuelTank) {
						return ((FuelTank)p).fuel;
					}
					if(p is RCSFuelTank) {
						return ((RCSFuelTank)p).fuel;
					}
	            }
	        }
             */
			return 0;
		}
	}
}
