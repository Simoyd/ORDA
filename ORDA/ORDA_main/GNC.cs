using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class GNC
	{
		// settings
		public const float Default_Kp_AngVel = 0.25f;
		public const float Default_Kp_AngAcc = 10.0f;
		public const float Default_Kp_Vel = 0.5f;
		public const float Default_Kp_Acc = 1.0f;

		public const float Default_eacPulseLength = 0.1f;	// [s]
		public const float Default_eacPulseLevel = 1.0f;
		public const float Default_eacRate = 10.0f;			// [°/s]

		public const float dockAbortAttitude = 2.0f;
		public const float dockAbortDeviation = 2.0f;
		public const float dockAbortLatchMiss = 0.3f;

		const float positionModeDistance = 50.0f;
		Vector3 dockEntryPoint = new Vector3(0, 50, 0);
		const float dockStateTransitionDelay = 2.5f;

		// connectivity
		FlightData flightData = null;
		Bus bus = null;

		// states
		public enum Command { OFF=0, RATE, ATT, EAC, DOCK, AWES };
		public enum RateMode { IDLE=0, ZERO, ROLL, HOLD };
        public enum AttMode { IDLE = 0, REF, HOLD, VP, VN, NP, NN, RP, RN, RPP, RPN, RVP, RVN, RCP, RCN, NODE };
        public enum UpMode { IDLE = 0, NP, NN, RP, RN };
        public enum PosMode { IDLE = 0, ZERO, HOLD, VN, RN, RETREAT };
		public enum EACMode { IDLE=0, PULSE, RATE };
		public enum DockMode { IDLE=0, ATTITUDE, AUTO };
		public enum DockState { IDLE=0, ENTRY, ORIENT, APPROACH, DOCKED, DEPART, ABORT };
		public enum DockAbort { UNKNOWN=0, ATTITUDE, DEVIATION, LATCH };

		Command command = Command.OFF;
		RateMode rateMode = RateMode.IDLE;
        AttMode attMode = AttMode.IDLE;
        UpMode upMode = UpMode.IDLE;
        EACMode eacMode = EACMode.IDLE;
		PosMode posMode = PosMode.IDLE;
		DockMode dockMode = DockMode.IDLE;
		DockState dockState = DockState.IDLE;
		DockAbort dockAbort = DockAbort.UNKNOWN;
		float dockStateTimer = 0;

		// hold settings
		bool userRateHoldRequest = false;
		bool userAttHoldRequest = false;
		bool userPosHoldRequest = false;
		Vector3 userRateSetting = Vector3.zero;
		Vector3 userAttSetting = Vector3.zero;
		Vector3 userAttUpSetting = Vector3.zero;
		Vector3 userPosSetting = Vector3.zero;

		// ang/lin controller settings
		float Kp_AngVel = Default_Kp_AngVel;
		float Kp_AngAcc = Default_Kp_AngAcc;
		float Kp_Vel = Default_Kp_Vel;
		float Kp_Acc = Default_Kp_Acc;

		// controller inputs
		bool attActive = false;
		bool attUpActive = false;
		bool avelActive = false;
		bool rposActive = false; // hold relative position in inertial frame
		bool rposDock = false;   // use docking port CoM instead of vessel CoM
		bool rvelActive = false; // hold relative velocity in ship frame
		bool rvelLimitMin = false;
		bool rvelLimitMax = false;

		Vector3 attCommand = Vector3.zero;
		Vector3 attUpCommand = Vector3.zero;
		Vector3 avelCommand = Vector3.zero;
		Vector3 aaccCommand = Vector3.zero;
		Vector3 rposCommand = Vector3.zero;
		Vector3 rvelCommand = Vector3.zero;
		float rvelLimit = 0;
		Vector3 accCommand = Vector3.zero;

		// controller status
		public Vector3 attError = Vector3.zero;
		public Vector3 avelError = Vector3.zero;
		public Vector3 rposError = Vector3.zero;
		public Vector3 rvelError = Vector3.zero;
		public Vector3 dockDeviationAngle = Vector3.zero;

		// eac settings & state
		float eacPulseLength = Default_eacPulseLength;
		float eacPulseLevel = Default_eacPulseLevel;
		float eacRate = Default_eacRate;
		Vector3 pulsePrevYPR = Vector3.zero;
		Vector3 pulseTimes = Vector3.zero;

		// dock settings
		bool dockInvertUp = false;

		//
		// public methods
		//
		public GNC (FlightData fd, Bus b)
		{
			flightData = fd;
			bus = b;
		}

		public void getStates (out Command outCommand, 
		                       out RateMode outRateMode, 
		                       out AttMode outAttMode,
                               out UpMode outUpMode,
		                       out EACMode outEacMode,
		                       out PosMode outPosMode,
		                       out DockMode outDockMode)
		{
			outCommand = command;
			outRateMode = rateMode;
			outAttMode = attMode;
            outUpMode = upMode;
			outEacMode = eacMode;
			outPosMode = posMode;
			outDockMode = dockMode;
		}

		public void getDockState (out DockState outDockState,
		                          out DockAbort outDockAbort)
		{
			outDockState = dockState;
			outDockAbort = dockAbort;
		}

		public void getControllerSettings (out float outAngVel,
		                                   out float outAngAcc, 
		                                   out float outVel, 
		                                   out float outAcc)
		{
			outAngVel = Kp_AngVel;
			outAngAcc = Kp_AngAcc;
			outVel = Kp_Vel;
			outAcc = Kp_Acc;
		}

		public void setControllerSettings (float angVel,
		                                   float angAcc,
		                                   float vel,
		                                   float acc)
		{
			Kp_AngVel = angVel;
			Kp_AngAcc = angAcc;
			Kp_Vel = vel;
			Kp_Acc = acc;
		}

		public void getEACSettings (out float pulseLength,
		                            out float pulseLevel,
		                            out float rate)
		{
			pulseLength = eacPulseLength;
			pulseLevel = eacPulseLevel;
			rate = eacRate;
		}

		public void setEACSettings (float pulseLength,
		                            float pulseLevel,
		                            float rate)
		{
			eacPulseLength = pulseLength;
			eacPulseLevel = pulseLevel;
			eacRate = rate;
		}

		public void getDockSettings (out bool outInvertUp)
		{
			outInvertUp = dockInvertUp;
		}

		public void setDockSettings (bool invertUp)
		{
			dockInvertUp = invertUp;
		}

		public void requestCommand (Command c)
		{
			command = c;

			rateMode = RateMode.IDLE;
			attMode = AttMode.IDLE;
            upMode = UpMode.IDLE;
			eacMode = EACMode.IDLE;
			dockMode = DockMode.IDLE;
			dockState = DockState.IDLE;

			if (command != Command.RATE && command != Command.ATT && command != Command.EAC)
				posMode = PosMode.IDLE;

			if (command == Command.DOCK && flightData.targetVessel == null)
				command = Command.OFF;
		}

		public void requestRateMode (RateMode m)
		{
			if(command != Command.RATE)
				return;

			if(rateMode == m)
				rateMode = RateMode.IDLE;
			else
				rateMode = m;

			if(rateMode == RateMode.HOLD)
				userRateHoldRequest = true;
		}

		public void requestAttMode (AttMode m)
		{
			if(command != Command.ATT)
				return;

            if (attMode == AttMode.IDLE)
            {
                upMode = UpMode.IDLE;
            }

			if(attMode == m)
				attMode = AttMode.IDLE;
			else
				attMode = m;

			if(attMode == AttMode.HOLD)
				userAttHoldRequest = true;

            if (((attMode == AttMode.HOLD) || (attMode == AttMode.REF)) && (upMode != UpMode.IDLE))
            {
                upMode = UpMode.IDLE;
            }
		}

        public void requestUpMode(UpMode m)
        {
            if (command != Command.ATT)
                return;

            if (upMode == m)
                upMode = UpMode.IDLE;
            else
                upMode = m;

            if (((attMode == AttMode.HOLD) || (attMode == AttMode.REF)) && (upMode != UpMode.IDLE))
            {
                attMode = AttMode.IDLE;
            }
        }

		public void requestEacMode (EACMode m)
		{
			if(command != Command.EAC)
				return;

			if(eacMode == m)
				eacMode = EACMode.IDLE;
			else
				eacMode = m;
		}

		public void requestPosMode (PosMode m)
		{
			if(command == Command.DOCK || command == Command.AWES)
				return;

			if(posMode == m)
				posMode = PosMode.IDLE;
			else
				posMode = m;

			if(posMode == PosMode.HOLD)
				userPosHoldRequest = true;
		}

		public void requestDockMode (DockMode m)
		{
			if (command != Command.DOCK)
				return;

			if (dockMode == m)
				dockMode = DockMode.IDLE;
			else
				dockMode = m;

			dockState = DockState.IDLE;
			dockAbort = DockAbort.UNKNOWN;
		}

		public void requestDockEngage ()
		{
			if(command != Command.DOCK)
				return;

			if(dockMode != DockMode.AUTO)
				return;

			dockState = DockState.ENTRY;
			dockAbort = DockAbort.UNKNOWN;
			dockStateTimer = 0;
		}

		public void getConfiguration (out GNCconfig config)
		{
			config = new GNCconfig();

			config.command = command;
			config.rateMode = rateMode;
			config.attMode = attMode;
            config.upMode = upMode;
			config.eacMode = eacMode;
			config.posMode = posMode;
			config.dockMode = dockMode;
			config.dockState = dockState;
			config.dockAbort = dockAbort;
			config.userRateSetting = new sVector3(userRateSetting);
			config.userAttSetting = new sVector3(userAttSetting);
			config.userAttUpSetting = new sVector3(userAttUpSetting);
			config.userPosSetting = new sVector3(userPosSetting);
			config.Kp_AngVel = Kp_AngVel;
			config.Kp_AngAcc = Kp_AngAcc;
			config.Kp_Vel = Kp_Vel;
			config.Kp_Acc = Kp_Acc;
			config.eacPulseLength = eacPulseLength;
			config.eacPulseLevel = eacPulseLevel;
			config.eacRate = eacRate;
			config.dockInvertUp = dockInvertUp;
		}

		public void restoreConfiguration (GNCconfig config)
		{
			// should be good, might need to add some checks
			command = config.command;
			rateMode = config.rateMode;
			attMode = config.attMode;
            upMode = config.upMode;
			eacMode = config.eacMode;
			posMode = config.posMode;
			dockMode = config.dockMode;
			dockState = config.dockState;
			dockAbort = config.dockAbort;
			userRateSetting = config.userRateSetting.toVector3();
			userAttSetting = config.userAttSetting.toVector3();
			userAttUpSetting = config.userAttUpSetting.toVector3();
			userPosSetting = config.userPosSetting.toVector3();
			Kp_AngVel = config.Kp_AngVel;
			Kp_AngAcc = config.Kp_AngAcc;
			Kp_Vel = config.Kp_Vel;
			Kp_Acc = config.Kp_Acc;
			eacPulseLength = config.eacPulseLength;
			eacPulseLevel = config.eacPulseLevel;
			eacRate = config.eacRate;
			dockInvertUp = config.dockInvertUp;
		}

		public void update (float dt)
		{
			// check states
			checkStates ();

			// reset commanding/stats
			attActive = false;
			attUpActive = false;
			avelActive = false;
			rposActive = false;
			rposDock = false;
			rvelActive = false;
			rvelLimitMin = false;
			rvelLimitMax = false;

			attCommand = Vector3.zero;
			attUpCommand = Vector3.zero;
			avelCommand = Vector3.zero;
			aaccCommand = Vector3.zero;
			rposCommand = Vector3.zero;
			rvelCommand = Vector3.zero;
			rvelLimit = 0;

			dockDeviationAngle = Vector3.zero;

			// rate hold mastermode
			if (command == Command.RATE) {
				rateLogic ();
			}
			// attitude hold mastermode
			else if (command == Command.ATT) {
				attLogic ();
			}
			// enhanced attitude control
			else if (command == Command.EAC) {
				eacLogic ();
			}
			// docking mastermode
			else if (command == Command.DOCK) {
				dockLogic (dt);
			}
            else if (command == Command.AWES)
            {
                awesLogic();
            }

			// position modes
			if (command != Command.DOCK && command != Command.AWES) {
				positionLogic ();
			}

			// ang/lin controller
			controller();
		}

		//
		// private methods
		//
		private void checkStates ()
		{
			// docking override?
			if (command == Command.DOCK || command == Command.AWES) {
				attMode = AttMode.IDLE;
                upMode = UpMode.IDLE;
				rateMode = RateMode.IDLE;
				posMode = PosMode.IDLE;
			}

			// no target vessel?
			if (flightData.targetVessel == null) {

				// disable invalid att hold modes
				if (command == Command.ATT &&
					(attMode == AttMode.RPP || attMode == AttMode.RPN ||
					attMode == AttMode.RVP || attMode == AttMode.RVN ||
                    attMode == AttMode.RCP || attMode == AttMode.RCN)) {
					requestAttMode (AttMode.IDLE);
				}
			}

			// no target vessel or target changed?
			if (flightData.targetVessel == null || flightData.targetChanged) {

				// disable position modes
				if (posMode != PosMode.IDLE) {
					requestPosMode (PosMode.IDLE);
				}
			}

			// no target vessel or docking ports?
			if (flightData.targetVessel == null || 
			    flightData.targetPart == null || 
			    flightData.vesselPart == null ||
			    flightData.targetChanged) {

				// disable dock master mode
				if(command == Command.DOCK)
				{
					requestCommand(Command.OFF);
				}
			}
		}

		private void rateLogic ()
		{
			// process hold request
			if (userRateHoldRequest) {
				userRateHoldRequest = false;
				userRateSetting = flightData.angularVelocity;
			}

			avelActive = true;
			switch (rateMode) {
			case RateMode.ZERO:
				avelCommand = Vector3.zero;
				break;
			case RateMode.ROLL:
				avelCommand = new Vector3 (0, 0.5f, 0);
				break;
			case RateMode.HOLD:
				avelCommand = userRateSetting;
				break;
			default:
				avelActive = false;
				break;
			}

			float distance = rposError.magnitude;
			if (distance < 100) {
				rvelLimitMax = true;
				rvelLimit = 1.0f;
			}
		}

		private void attLogic ()
		{
			// process hold request
			if (userAttHoldRequest) {
				userAttHoldRequest = false;
                userAttSetting = flightData.vessel.GetTransform().TransformDirection(new Vector3(0, 1, 0));
                userAttUpSetting = flightData.vessel.GetTransform().TransformDirection(new Vector3(0, 0, -1));
			}

			attActive = true;
            switch (attMode)
            {
                case AttMode.REF:
                    attUpActive = true;
                    attCommand = new Vector3(1, 0, 0);
                    attUpCommand = new Vector3(0, 1, 0);
                    break;
                case AttMode.HOLD:
                    attUpActive = true;
                    attCommand = userAttSetting;
                    attUpCommand = userAttUpSetting;
                    break;
                case AttMode.VP:
                    attCommand = flightData.orbitVelocity.normalized;
                    break;
                case AttMode.VN:
                    attCommand = -flightData.orbitVelocity.normalized;
                    break;
                case AttMode.NP:
                    attCommand = flightData.orbitNormal.normalized;
                    break;
                case AttMode.NN:
                    attCommand = -flightData.orbitNormal.normalized;
                    break;
                case AttMode.RP:
                    attCommand = flightData.orbitUp.normalized;
                    break;
                case AttMode.RN:
                    attCommand = -flightData.orbitUp.normalized;
                    break;
                case AttMode.RPP:
                    attCommand = flightData.targetRelPosition.normalized;
                    break;
                case AttMode.RPN:
                    attCommand = -flightData.targetRelPosition.normalized;
                    break;
                case AttMode.RVP:
                    attCommand = flightData.targetRelVelocity.normalized;
                    break;
                case AttMode.RVN:
                    attCommand = -flightData.targetRelVelocity.normalized;
                    break;
                case AttMode.RCP:
                    attCommand = (flightData.targetRelPosition.normalized - flightData.targetRelVelocity.normalized).normalized;
                    break;
                case AttMode.RCN:
                    attCommand = (-flightData.targetRelPosition.normalized - flightData.targetRelVelocity.normalized).normalized;
                    break;
                case AttMode.NODE:
                    if (flightData.firstNodeBurnVector.HasValue)
                    {
                        attCommand = flightData.firstNodeBurnVector.Value;
                    }
                    else
                    {
                        attActive = false;
                        requestAttMode(AttMode.IDLE);
                    }
                    break;
                default:
                    attActive = false;
                    break;
            }

            if ((attMode != AttMode.HOLD) && (attMode != AttMode.REF))
            {
                switch(upMode)
                {
                    case UpMode.RP:
                        attUpActive = true;
                        attUpCommand = flightData.orbitUp.normalized;
                        break;
                    case UpMode.RN:
                        attUpActive = true;
                        attUpCommand = -flightData.orbitUp.normalized;
                        break;
                    case UpMode.NP:
                        attUpActive = true;
                        attUpCommand = flightData.orbitNormal.normalized;
                        break;
                    case UpMode.NN:
                        attUpActive = true;
                        attUpCommand = -flightData.orbitNormal.normalized;
                        break;
                }
            }
		}

		private void eacLogic ()
		{
			const float threshold = 0.1f;
			float y = (Mathf.Abs(bus.yawReq) > threshold) ? (Mathf.Sign(bus.yawReq)) : (0);
			float p = (Mathf.Abs(bus.pitchReq) > threshold) ? (Mathf.Sign(bus.pitchReq)) : (0);
			float r = (Mathf.Abs(bus.rollReq) > threshold) ? (Mathf.Sign(bus.rollReq)) : (0);
			float dt = Time.fixedDeltaTime; // flightData.dt;

			if (eacMode == EACMode.PULSE) {

				// look for transition
				if(Mathf.Abs(pulsePrevYPR.x - y) > threshold/2)
					pulseTimes.x = eacPulseLength;
				if(Mathf.Abs(pulsePrevYPR.y - p) > threshold/2)
					pulseTimes.y = eacPulseLength;
				if(Mathf.Abs(pulsePrevYPR.z - r) > threshold/2)
					pulseTimes.z = eacPulseLength;
				pulsePrevYPR = new Vector3(y, p, r);

				// drive bus
				bus.yprDriven = true;
				bus.yprRelative = false;
				if(pulseTimes.x > dt) {
					pulseTimes.x -= dt;
					bus.yaw = y * eacPulseLevel;
				}
				if(pulseTimes.y > dt) {
					pulseTimes.y -= dt;
					bus.pitch = p * eacPulseLevel;
				}
				if(pulseTimes.z > dt) {
					pulseTimes.z -= dt;
					bus.roll = r * eacPulseLevel;
				}

			} else if (eacMode == EACMode.RATE) {

				// command angular velocity
				bus.yprRelative = false;
				avelActive = true;
				avelCommand = new Vector3(-p, -r, -y) * (eacRate / 180 * Mathf.PI);
			}
		}

		private void positionLogic ()
		{
			// process hold request
			if (userPosHoldRequest) {
				userPosHoldRequest = false;
				userPosSetting = flightData.targetRelPosition;
			}

			if (posMode == PosMode.ZERO) {
				rvelActive = true;
				rvelCommand = Vector3.zero;
			} else if (posMode == PosMode.HOLD) {
				rposActive = true;
				rposCommand = userPosSetting;
			} else if (posMode == PosMode.VN) {
				rposActive = true;
				rposCommand = Util.reorder (flightData.targetVessel.orbit.vel, 132).normalized * positionModeDistance;
			} else if (posMode == PosMode.RN) {
				rposActive = true;
				rposCommand = Util.reorder (flightData.targetVessel.orbit.pos, 132).normalized * positionModeDistance;
			} else if (posMode == PosMode.RETREAT) {
				rvelActive = true;
				rvelCommand = flightData.targetRelPositionShip.normalized * 1.0f;
				if (flightData.targetRelPosition.magnitude > 50) {
					posMode = PosMode.ZERO;
				}
			}

			if (rposActive) {
				// limit to save some rcs fuel
				rvelLimitMax = true;
				float dist = rposError.magnitude;
				if (dist < 100) {
					rvelLimit = 1.0f;
				} else {
					rvelLimit = 5.0f;
				}
			}
		}

		private void dockLogic (float dt)
		{
            // [CW] Pretty sure this is all broken, so just return
            return;

			// maintain docking attitude
			if(dockMode == DockMode.ATTITUDE) {

				// before we do anything else - docked?
				if (((ORDA_dock)flightData.vesselPart).isDocked ()) {
					dockMode = DockMode.IDLE;
				} else {
					// command alignment
					Vector3 forward = flightData.targetPart.transform.TransformDirection(new Vector3(0, 1, 0));
					Vector3 up = flightData.targetPart.transform.TransformDirection(new Vector3(0, 0, 1));
					attActive = true;
					attUpActive = true;
					attCommand = -forward;
					attUpCommand = -up;

					// special case for docking ports facing the wrong direction
					if ((flightData.vesselPart.transform.TransformDirection(new Vector3(0, 1, 0)) -
                         flightData.vessel.GetTransform().TransformDirection(new Vector3(0, 1, 0))).magnitude > 0.5f)
                    {
						attCommand = forward;
						attUpCommand = up;
					}
					if(dockInvertUp) attUpCommand *= -1;
				}
			}
			// fully autonomous docking
			else if(dockMode == DockMode.AUTO) {

				// before we do anything else - docked?
				if (((ORDA_dock)flightData.vesselPart).isDocked ()) {
					dockState = DockState.DOCKED;
				}

				// get relative dock position and attitude error
				ORDA_dock dock = (ORDA_dock)flightData.vesselPart;
				Vector3 relPos; // vesselport -> targetport in ship frame
				float distance;
				Vector3 euler;
				dock.getRelPosAndAtt (flightData.targetPart, out relPos, out distance, out euler);

				// calculate targets deviation from our centerline
				float deviationDistance = relPos.magnitude;
				if (deviationDistance < 1.0f)
					deviationDistance = 1.0f;
				dockDeviationAngle.x = Mathf.Asin (Mathf.Abs (relPos.x) / deviationDistance) * Mathf.Rad2Deg;
				dockDeviationAngle.z = Mathf.Asin (Mathf.Abs (relPos.z) / deviationDistance) * Mathf.Rad2Deg;

				// handle states
				if(dockState == DockState.ENTRY) {

					// minimize rotation
					avelActive = true;
					avelCommand = Vector3.zero;

					// move to entry
					rposActive = true;
					rposDock = true;
					rposCommand = dockEntryPoint;

					// next state?
					if(Util.maxElement(rposError) < 0.1f) {
						dockStateTimer += dt;
						if(dockStateTimer > dockStateTransitionDelay) {
							dockState = DockState.ORIENT;
							dockStateTimer = 0;
						}
					} else {
						dockStateTimer = 0;
					}
				}
				else if(dockState == DockState.ORIENT) {

					// stay on entry
					rposActive = true;
					rposDock = true;
					rposCommand = dockEntryPoint;

					// command alignment
					Vector3 forward = flightData.targetPart.transform.TransformDirection(new Vector3(0, 1, 0));
					Vector3 up = flightData.targetPart.transform.TransformDirection(new Vector3(0, 0, 1));
					attActive = true;
					attUpActive = true;
					attCommand = -forward;
					attUpCommand = -up;

					// special case for docking ports facing the wrong direction
					if ((flightData.vesselPart.transform.TransformDirection(new Vector3(0, 1, 0)) -
                         flightData.vessel.GetTransform().TransformDirection(new Vector3(0, 1, 0))).magnitude > 0.5f)
                    {
						attCommand = forward;
						attUpCommand = up;
					}
					if(dockInvertUp) attUpCommand *= -1;

					// next state?
					if(Util.maxElement(rposError) < 0.1f && Util.maxElement(euler) < 0.1f) {
						dockStateTimer += dt;
						if(dockStateTimer > dockStateTransitionDelay) {
							dockState = DockState.APPROACH;
							dockStateTimer = 0;
						}
					} else {
						dockStateTimer = 0;
					}
				}
				else if(dockState == DockState.APPROACH) {

					// approach
					rposActive = true;
					rposDock = true;
					rposCommand = Vector3.zero;

					// maintain alignment
					Vector3 forward = flightData.targetPart.transform.TransformDirection(new Vector3(0, 1, 0));
					Vector3 up = flightData.targetPart.transform.TransformDirection(new Vector3(0, 0, 1));
					attActive = true;
					attUpActive = true;
					attCommand = -forward;
					attUpCommand = -up;

					// special case for docking ports facing the wrong direction
					if ((flightData.vesselPart.transform.TransformDirection(new Vector3(0, 1, 0)) -
                         flightData.vessel.GetTransform().TransformDirection(new Vector3(0, 1, 0))).magnitude > 0.5f)
                    {
						attCommand = forward;
						attUpCommand = up;
					}
					if(dockInvertUp) attUpCommand *= -1;

					// abort?
					if(Util.maxElement(dockDeviationAngle) > dockAbortDeviation) {
						dockState = DockState.ABORT;
						dockAbort = DockAbort.DEVIATION;
						dockStateTimer = 0;
					}
					if(Util.maxElement(euler) > dockAbortAttitude) {
						dockState = DockState.ABORT;
						dockAbort = DockAbort.ATTITUDE;
						dockStateTimer = 0;
					}
					if (relPos.y < -dockAbortLatchMiss) {
						dockState = DockState.ABORT;
						dockAbort = DockAbort.LATCH;
						dockStateTimer = 0;
					}

					// limit approach velocity
					rvelLimitMax = true;
					float dist = rposError.magnitude;
					if(dist < 2.5f) {
						rvelLimitMin = true;
						rvelLimit = 0.1f;
					}
					else if(dist < 5.0f) {
						rvelLimit = 0.25f;
					}
					else if(dist < 10.0f) {
						rvelLimit = 0.5f;
					} else {
						rvelLimit = 1.0f;
					}
				}
				else if(dockState == DockState.DOCKED) {

					// undocked?
					if (((ORDA_dock)flightData.vesselPart).isDocked () == false) {
						dockState = DockState.DEPART;
						dockStateTimer = 0;
					}
				}
				else if(dockState == DockState.DEPART) {

					// free drift a bit first
					if(distance > 2.5f) {

						// minimize rotation
						avelActive = true;
						avelCommand = Vector3.zero;

						// move to entry
						rposActive = true;
						rposDock = true;
						rposCommand = dockEntryPoint;
					}
				}
				else if(dockState == DockState.ABORT) {

					// minimize rotation
					avelActive = true;
					avelCommand = Vector3.zero;

					// move to entry
					rposActive = true;
					rposDock = true;
					rposCommand = dockEntryPoint;
				}
			}
		}

        // [CW]TODO: Implement this
        private void awesLogic()
        {
            float startAlt = 15000;
            float endAlt = 70000;

            //attActive = true;
            //attUpActive = true;
            //attCommand = ?vector;
            //attUpCommand = ?vector;

            Vector3 up = flightData.orbitUp.normalized;
            Vector3 forward = Vector3.Cross(flightData.planetZUpAngularVelocity.normalized, flightData.orbitUp.normalized).normalized;

            float percent = (flightData.altitudeASL - startAlt) / (endAlt - startAlt);

            if (percent < 0)
            {
                percent = 0;
                attActive = true;
                attCommand = up;
            }
            else if (percent > 1)
            {
                percent = 1;
                attActive = true;
                attCommand = forward;
                attUpActive = true;
                attUpCommand = up;
            }
            else
            {
                float percentAngle = percent * (Mathf.PI / 3) + (Mathf.PI / 6);
                Vector3 actualUp = up.normalized * Mathf.Cos(percentAngle);
                Vector3 actualForward = forward.normalized * Mathf.Sin(percentAngle);

                attActive = true;
                attCommand = actualUp + actualForward;
            }

            //Vector3 woobleNormal = Vector3.Cross(flightData.vessel.GetTransform().up, flightData.totalThrustVector);
            //float woobleAngle = Vector3.Angle(flightData.vessel.GetTransform().up, flightData.totalThrustVector);

            //attCommand = Quaternion.AngleAxis(woobleAngle * flightData.woobleFactor, woobleNormal) * attCommand;
        }

        private int lastStart;
        
        private void controller()
        {
            //#region Timer Limiter

            //bool go = false;

            //int curTime = System.Environment.TickCount;

            //if ((curTime - lastStart) > 1000)
            //{
            //    lastStart = curTime;
            //}

            //if ((curTime - lastStart) < 100)
            //{
            //    go = true;
            //}

            //if (!go)
            //{
            //    return;
            //}

            //#endregion

            attError = Vector3.zero;
			avelError = Vector3.zero;
			rposError = Vector3.zero;
			rvelError = Vector3.zero;

			if (attActive) {
                Vector3 error = flightData.vessel.GetTransform().InverseTransformDirection(attCommand);
				attError = error;
                flightData.attError = attCommand;

				float p = Mathf.Atan2 (error.z, error.y);
				float r = 0;
				float y = -Mathf.Atan2 (error.x, error.y);

				if (attUpActive)
                {
					if (Mathf.Abs (p) / Mathf.PI * 180 < 10 &&
						Mathf.Abs (y) / Mathf.PI * 180 < 10)
                    {
                        Vector3 up = flightData.vessel.GetTransform().InverseTransformDirection(-attUpCommand);
						r = Mathf.Asin (up.x);
					}
				}

				float ps = (p < 0) ? (-1) : (1);
				float rs = (r < 0) ? (-1) : (1);
				float ys = (y < 0) ? (-1) : (1);

				// w = sqrt( 2 * phi * (dw/dt) )
				avelActive = true;
				Vector3 angAcc = flightData.availableAngAcc;
				avelCommand.x = ps * Mathf.Sqrt (2 * Mathf.Abs (p) * angAcc.x) * Kp_AngVel;
				avelCommand.y = rs * Mathf.Sqrt (2 * Mathf.Abs (r) * angAcc.y) * Kp_AngVel;
				avelCommand.z = ys * Mathf.Sqrt (2 * Mathf.Abs (y) * angAcc.z) * Kp_AngVel;
			}

			if (avelActive) {
				Vector3 error = avelCommand - flightData.angularVelocity;
				avelError = error;
				aaccCommand = -error * Kp_AngAcc;

				// T = I * (dw/dt)
				float Tp = flightData.MoI.x * aaccCommand.x;
				float Tr = flightData.MoI.y * aaccCommand.y;
				float Ty = flightData.MoI.z * aaccCommand.z;

				bus.yprDriven = true;
                bus.pitch = Mathf.Clamp(Tp / flightData.availableTorque.x, -1.0f, 1.0f);
                bus.roll = Mathf.Clamp(Tr / flightData.availableTorque.y, -1.0f, 1.0f);
                bus.yaw = Mathf.Clamp(Ty / flightData.availableTorque.z, -1.0f, 1.0f);
			}

			if (rposActive) {
				Vector3 error = rposCommand - flightData.targetRelPosition;
				if(rposDock) {
					Vector3 pos = flightData.targetPart.transform.position +
								  flightData.targetPart.transform.TransformDirection (rposCommand);
					Vector3 relPos = pos - flightData.vesselPart.transform.position;
					error = -relPos;
				}
				rposError = error;

                Vector3 shipFrameError = flightData.vessel.GetTransform().InverseTransformDirection(error);
				float dx = shipFrameError.x;
				float dy = shipFrameError.y;
				float dz = shipFrameError.z;
				float absx = Mathf.Abs (dx);
				float absy = Mathf.Abs (dy);
				float absz = Mathf.Abs (dz);
				float sx = (dx > 0) ? (1) : (-1);
				float sy = (dy > 0) ? (1) : (-1);
				float sz = (dz > 0) ? (1) : (-1);

				// v = sqrt( 2 * error * a )
				rvelActive = true;
				Vector3 linAcc = flightData.availableLinAcc;
				rvelCommand.x = sx * Mathf.Sqrt (2 * absx * linAcc.x) * Kp_Vel;
				rvelCommand.y = sy * Mathf.Sqrt (2 * absy * linAcc.y) * Kp_Vel;
				rvelCommand.z = sz * Mathf.Sqrt (2 * absz * linAcc.z) * Kp_Vel;

				// limit?
				float mag = rvelCommand.magnitude;
				Vector3 n = rvelCommand.normalized;
				if((rvelLimitMax && mag > rvelLimit) || (rvelLimitMin && mag < rvelLimit)) {
					rvelCommand = n * rvelLimit;
				}
			}

			if (rvelActive) {
				Vector3 error = rvelCommand - flightData.targetRelVelocityShip;
				rvelError = error;
				accCommand = error * Kp_Acc;

				// F = m * a
				float fx = flightData.mass * accCommand.x;
				float fy = flightData.mass * accCommand.y;
				float fz = flightData.mass * accCommand.z;

				bus.xyzDriven = true;
				bus.x = Mathf.Clamp (fx / flightData.availableForce.x, -1.0f, +1.0f);
				bus.y = Mathf.Clamp (fy / flightData.availableForce.y, -1.0f, +1.0f);
				bus.z = Mathf.Clamp (fz / flightData.availableForce.z, -1.0f, +1.0f);
			}
		}
	}

	[Serializable()]
	public class GNCconfig
	{
		public GNC.Command command;
		public GNC.RateMode rateMode;
        public GNC.AttMode attMode;
        public GNC.UpMode upMode;
        public GNC.EACMode eacMode;
		public GNC.PosMode posMode;
		public GNC.DockMode dockMode;
		public GNC.DockState dockState;
		public GNC.DockAbort dockAbort;
		public sVector3 userRateSetting;
		public sVector3 userAttSetting;
		public sVector3 userAttUpSetting;
		public sVector3 userPosSetting;
		public float Kp_AngVel;
		public float Kp_AngAcc;
		public float Kp_Vel;
		public float Kp_Acc;
		public float eacPulseLength;
		public float eacPulseLevel;
		public float eacRate;
		public bool dockInvertUp;
	}
}
