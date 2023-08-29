namespace moveen.core {
    using System;
    using System.Collections.Generic;
    using moveen.descs;
    using moveen.utils;
    using UnityEngine;

    [Serializable] public class Stepper5 {

        [Tooltip("Logical right leg to calculate gait (even for multipeds)")] public int leadingLegRight;
        [Tooltip("Logical left leg to calculate gait (even for multipeds)")] public int leadingLegLeft = 1;
        [Range(0,1)][Tooltip("Step phase. 0.5 - normal step. 0.1 - left right pause. 0.9 - right left pause")] public float phase = 0.5f;
        [Range(0,1)][Tooltip("The leg will try to get more support from behind")] public float lackOfSpeedCompensation = 0.1f;
        public float rewardSelf = 2;
        public float rewardOthers = 2;
        public float affectOthers;
        public float rewardPare = 5;
        public float affectCounter = 20;
        [HideInInspector] public float runJumpTime;
        public bool forceBipedalEarlyStep;
        [Tooltip("Reduce foot entanglement for bipedals")] public bool bipedalForbidPlacement;
        [Tooltip("Protects the body from fall through. Must be enabled if no colliders is used")] public bool protectBodyFromFallthrough = true;
        [Tooltip("Ceiling height which will not be seen as a floor, through which it fell. Don't make it too small, as it is critical on steep slopes")] public float protectBodyFromFallthroughMaxHeight = 1;
        [Header("Body movement")][Range(0.5f,1.5f)][Tooltip("0.5 - lower body between lands, 1 - no lowering, 1.5 - higher between lands (unnatural)")] public float downOnStep = 0.7f;
        public MotorBean horizontalMotor;
        public MotorBean verticalMotor;
        public MotorBean rotationMotor;
        [Header("Center Of Gravity simulation (important for certain gait)")][Tooltip("Center Of Gravity")] public float cogUpDown;
        [Range(-0.5f,0.5f)][Tooltip("Rotate around Center Of Gravity")] public float cogAngle = 0.2f;
        public float cogRotationMultiplier = 1;
        [Tooltip("Push acceleration to compensate Center Of Gravity")] public float cogAccel = 10;
        [Header("Body helps or opposes legs position")][Range(-1,1)][Tooltip("Rotation for the body to help steps length or oppose (-1 - clumsy, +1 - agile)")] public float bodyLenHelp;
        [Tooltip("Body helps the length in movement only")] public bool bodyLenHelpAtSpeedOnly = true;
        [Tooltip("Speed at which maximum rotation is achieved")] public float bodyLenHelpMaxSpeed = 1;
        [Header("Hip")][Range(0,0.5f)][Tooltip("Hip flexibility relative to the body")] public float hipFlexibility;
        [HideInInspector] public Quaternion wantedHipRot;
        Quaternion slowLocalHipRot;
        [Tooltip("Hip position relative to the body (center of its rotation)")] public Vector3 hipPosRel = new Vector3(0, -0.5f, 0);
        [HideInInspector] public Vector3 hipPosAbs;
        [HideInInspector] public Quaternion hipRotAbs = Quaternion.identity;
        [NonSerialized] public bool doTickHip;
        [Header("_system")] public bool collectSteppingHistory;
        public bool showPhaseDials;
        [HideInInspector] public Quaternion projectedRot;
        [HideInInspector] public Vector3 realSpeed;
        [HideInInspector] public Vector3 g = new Vector3(0, -9.81f, 0);
        [HideInInspector] public Vector3 realBodyAngularSpeed;
        [HideInInspector][InstrumentalInfo] public Vector3 resultAcceleration;
        [HideInInspector][InstrumentalInfo] public Vector3 resultRotAcceleration;
        [HideInInspector] public Vector3 realBodyPos;
        [HideInInspector] public Quaternion realBodyRot = Quaternion.identity;
        [HideInInspector] public Vector3 projPos;
        [HideInInspector][InstrumentalInfo] public Vector3 inputWantedPos;
        [HideInInspector] public Quaternion inputWantedRot;
        [HideInInspector][InstrumentalInfo] public Vector3 inputAnimPos;
        [HideInInspector] public Quaternion inputAnimRot;
        [NonSerialized] public ISurfaceDetector surfaceDetector = new SurfaceDetectorStub();
        [NonSerialized] public List<MoveenSkelBase> legSkel = MUtil2.al<MoveenSkelBase>();
        [NonSerialized] public List<Step2> steps = MUtil2.al<Step2>();
        [NonSerialized] public Vector3 up = new Vector3(0, 1, 0);
        [HideInInspector][InstrumentalInfo] public Vector3 imCenter;
        [HideInInspector][InstrumentalInfo] public Vector3 imCenterSpeed;
        [HideInInspector][InstrumentalInfo] public Vector3 imCenterAngularSpeed;
        [HideInInspector][InstrumentalInfo] public Vector3 imBody;
        [HideInInspector][InstrumentalInfo] public Vector3 imBodySpeed;
        [HideInInspector][InstrumentalInfo] public Vector3 imActualCenterSpeed;
        [HideInInspector][InstrumentalInfo] public Vector3 speedLack;
        [HideInInspector][InstrumentalInfo] public Vector3 virtualForLegs;
        [HideInInspector][InstrumentalInfo] public float midLen;

        public Stepper5() {
            MUtil.logEvent(this, "constructor");
        }
        public virtual void setWantedPos(float dt, Vector3 wantedPos, Quaternion wantedRot) {
            this.inputWantedPos = wantedPos;
            this.inputWantedRot = wantedRot;
            this.projPos = this.project(this.realBodyPos);
            this.projectedRot = MUtil.qToAxes(ExtensionMethods.getXForVerticalAxis(ExtensionMethods.rotate(wantedRot, new Vector3(1, 0, 0)), this.up), this.up);
        }
        public virtual void tick(float dt) {
            for (int i = 0; (i < this.steps.Count); (i)++)  {
                if ((this.steps[i].thisTransform == null))  {
                    this.legSkel.RemoveAt(i);
                    this.steps.RemoveAt(i);
                    (i)--;
                }
            }
            for (int i = 0; (i < this.steps.Count); (i)++)  {
                Step2 step = this.steps[i];
                step.collectSteppingHistory = this.collectSteppingHistory;
                if (this.collectSteppingHistory)  {
                    step.paramHistory.next();
                }
            }
            this.tickHip(dt);
            this.calcAbs(dt);
            Step2 right = (((this.leadingLegRight < this.steps.Count)) ? (this.steps[this.leadingLegRight]) : (null));
            Step2 left = (((this.leadingLegLeft < this.steps.Count)) ? (this.steps[this.leadingLegLeft]) : (null));
            if (((right != null) && (left != null)))  {
                float p0 = right.timedProgress;
                float p1 = left.timedProgress;
                float fr = MyMath.fract((p1 - p0));
                if ((fr > this.phase))  {
                    right.beFaster = 0.5f;
                    left.beFaster = 0;
                } else  {
                    if ((fr < this.phase))  {
                        right.beFaster = 0;
                        left.beFaster = 0.5f;
                    }
                }
                right.legSpeed *= (1 + right.beFaster);
                left.legSpeed *= (1 + left.beFaster);
            }
            this.tickSteps(dt);
        }
/*GENERATED*/        [Optimize]
/*GENERATED*/        void tickHip(float dt) {
/*GENERATED*/            Step2 right;
/*GENERATED*/            bool _728 = (this.leadingLegRight < this.steps.Count);
/*GENERATED*/            if (_728)  {
/*GENERATED*/                right = this.steps[this.leadingLegRight];
/*GENERATED*/            } else  {
/*GENERATED*/                right = null;
/*GENERATED*/            }
/*GENERATED*/            Step2 left;
/*GENERATED*/            bool _729 = (this.leadingLegLeft < this.steps.Count);
/*GENERATED*/            if (_729)  {
/*GENERATED*/                left = this.steps[this.leadingLegLeft];
/*GENERATED*/            } else  {
/*GENERATED*/                left = null;
/*GENERATED*/            }
/*GENERATED*/            bool _730 = ((left != null) && (right != null));
/*GENERATED*/            if (_730)  {
/*GENERATED*/                this.midLen = ((left.maxLen + right.maxLen) / 2);
/*GENERATED*/            }
/*GENERATED*/            this.wantedHipRot = this.projectedRot;
/*GENERATED*/            int dockedCount = 0;
/*GENERATED*/            int airCount = 0;
/*GENERATED*/            for (int i = 0; (i < this.steps.Count); i = (i + 1))  {
/*GENERATED*/                bool _738 = this.steps[i].dockedState;
/*GENERATED*/                if (_738)  {
/*GENERATED*/                    dockedCount = (dockedCount + 1);
/*GENERATED*/                } else  {
/*GENERATED*/                    airCount = (airCount + 1);
/*GENERATED*/                }
/*GENERATED*/            }
/*GENERATED*/            for (int i = 0; (i < this.steps.Count); i = (i + 1))  {
/*GENERATED*/                this.steps[i].canGoAir = (dockedCount == 0);
/*GENERATED*/            }
/*GENERATED*/            float i1459_y = this.cogUpDown;
/*GENERATED*/            Quaternion i758_THIS = this.realBodyRot;
/*GENERATED*/            float i762_x = (i758_THIS.x * 2);
/*GENERATED*/            float i764_z = (i758_THIS.z * 2);
/*GENERATED*/            Vector3 localCog = new Vector3();
/*GENERATED*/            localCog.x = (((i758_THIS.x * (i758_THIS.y * 2)) + -(i758_THIS.w * i764_z)) * i1459_y);
/*GENERATED*/            localCog.y = ((1 + -((i758_THIS.x * i762_x) + (i758_THIS.z * i764_z))) * i1459_y);
/*GENERATED*/            localCog.z = (((i758_THIS.y * i764_z) + (i758_THIS.w * i762_x)) * i1459_y);
/*GENERATED*/            float additionalSpeed_x = 0;
/*GENERATED*/            float additionalSpeed_y = 0;
/*GENERATED*/            float additionalSpeed_z = 0;
/*GENERATED*/            Quaternion wantedRot = this.inputWantedRot;
/*GENERATED*/            for (int i = 0; (i < this.steps.Count); i = (i + 1))  {
/*GENERATED*/                Step2 step = this.steps[i];
/*GENERATED*/                bool _739 = (!step.dockedState && !step.wasTooLong);
/*GENERATED*/                if (_739)  {
/*GENERATED*/                    Vector3 i931_a = step.comfortPosRel;
/*GENERATED*/                    Vector3 i932_b = this.up;
/*GENERATED*/                    Vector3 rollAxis = new Vector3(
/*GENERATED*/                        ((i931_a.y * i932_b.z) + -(i931_a.z * i932_b.y)), 
/*GENERATED*/                        ((i931_a.z * i932_b.x) + -(i931_a.x * i932_b.z)), 
/*GENERATED*/                        ((i931_a.x * i932_b.y) + -(i931_a.y * i932_b.x)));
/*GENERATED*/                    Quaternion rollQuaternion = Quaternion.AngleAxis((float)(((this.cogAngle / Math.PI) * 180)), rollAxis);
/*GENERATED*/                    Vector3 disp = (rollQuaternion.rotate(-localCog) + localCog).withSetY(0);
/*GENERATED*/                    Quaternion i937_THIS = this.realBodyRot;
/*GENERATED*/                    float i941_x = (i937_THIS.x * 2);
/*GENERATED*/                    float i942_y = (i937_THIS.y * 2);
/*GENERATED*/                    float i943_z = (i937_THIS.z * 2);
/*GENERATED*/                    float i944_xx = (i937_THIS.x * i941_x);
/*GENERATED*/                    float i945_yy = (i937_THIS.y * i942_y);
/*GENERATED*/                    float i946_zz = (i937_THIS.z * i943_z);
/*GENERATED*/                    float i947_xy = (i937_THIS.x * i942_y);
/*GENERATED*/                    float i948_xz = (i937_THIS.x * i943_z);
/*GENERATED*/                    float i949_yz = (i937_THIS.y * i943_z);
/*GENERATED*/                    float i950_wx = (i937_THIS.w * i941_x);
/*GENERATED*/                    float i951_wy = (i937_THIS.w * i942_y);
/*GENERATED*/                    float i952_wz = (i937_THIS.w * i943_z);
/*GENERATED*/                    float i955_d = this.cogAccel;
/*GENERATED*/                    additionalSpeed_x = (
/*GENERATED*/                        additionalSpeed_x + 
/*GENERATED*/                        (
/*GENERATED*/                        (
/*GENERATED*/                        ((1 + -(i945_yy + i946_zz)) * disp.x) + 
/*GENERATED*/                        ((i947_xy + -i952_wz) * disp.y) + 
/*GENERATED*/                        ((i948_xz + i951_wy) * disp.z)) * 
/*GENERATED*/                        i955_d));
/*GENERATED*/                    additionalSpeed_y = (
/*GENERATED*/                        additionalSpeed_y + 
/*GENERATED*/                        (
/*GENERATED*/                        (
/*GENERATED*/                        ((i947_xy + i952_wz) * disp.x) + 
/*GENERATED*/                        ((1 + -(i944_xx + i946_zz)) * disp.y) + 
/*GENERATED*/                        ((i949_yz + -i950_wx) * disp.z)) * 
/*GENERATED*/                        i955_d));
/*GENERATED*/                    additionalSpeed_z = (
/*GENERATED*/                        additionalSpeed_z + 
/*GENERATED*/                        (
/*GENERATED*/                        (
/*GENERATED*/                        ((i948_xz + -i951_wy) * disp.x) + 
/*GENERATED*/                        ((i949_yz + i950_wx) * disp.y) + 
/*GENERATED*/                        ((1 + -(i944_xx + i945_yy)) * disp.z)) * 
/*GENERATED*/                        i955_d));
/*GENERATED*/                    bool _740 = (this.cogRotationMultiplier != 1);
/*GENERATED*/                    if (_740)  {
/*GENERATED*/                        Quaternion newVar_702 = Quaternion.AngleAxis((float)((((this.cogAngle * this.cogRotationMultiplier) / Math.PI) * 180)), rollAxis);
/*GENERATED*/                        rollQuaternion = newVar_702;
/*GENERATED*/                    }
/*GENERATED*/                    float i958_lhs_x_1717 = wantedRot.x;
/*GENERATED*/                    float i958_lhs_y_1718 = wantedRot.y;
/*GENERATED*/                    float i958_lhs_z_1719 = wantedRot.z;
/*GENERATED*/                    float i958_lhs_w_1720 = wantedRot.w;
/*GENERATED*/                    float i959_rhs_x_1721 = rollQuaternion.x;
/*GENERATED*/                    float i959_rhs_y_1722 = rollQuaternion.y;
/*GENERATED*/                    float i959_rhs_z_1723 = rollQuaternion.z;
/*GENERATED*/                    float i959_rhs_w_1724 = rollQuaternion.w;
/*GENERATED*/                    wantedRot.x = (
/*GENERATED*/                        (i958_lhs_w_1720 * i959_rhs_x_1721) + 
/*GENERATED*/                        (i958_lhs_x_1717 * i959_rhs_w_1724) + 
/*GENERATED*/                        (i958_lhs_y_1718 * i959_rhs_z_1723) + 
/*GENERATED*/                        -(i958_lhs_z_1719 * i959_rhs_y_1722));
/*GENERATED*/                    wantedRot.y = (
/*GENERATED*/                        (i958_lhs_w_1720 * i959_rhs_y_1722) + 
/*GENERATED*/                        (i958_lhs_y_1718 * i959_rhs_w_1724) + 
/*GENERATED*/                        (i958_lhs_z_1719 * i959_rhs_x_1721) + 
/*GENERATED*/                        -(i958_lhs_x_1717 * i959_rhs_z_1723));
/*GENERATED*/                    wantedRot.z = (
/*GENERATED*/                        (i958_lhs_w_1720 * i959_rhs_z_1723) + 
/*GENERATED*/                        (i958_lhs_z_1719 * i959_rhs_w_1724) + 
/*GENERATED*/                        (i958_lhs_x_1717 * i959_rhs_y_1722) + 
/*GENERATED*/                        -(i958_lhs_y_1718 * i959_rhs_x_1721));
/*GENERATED*/                    wantedRot.w = (
/*GENERATED*/                        (i958_lhs_w_1720 * i959_rhs_w_1724) + 
/*GENERATED*/                        -(i958_lhs_x_1717 * i959_rhs_x_1721) + 
/*GENERATED*/                        -(i958_lhs_y_1718 * i959_rhs_y_1722) + 
/*GENERATED*/                        -(i958_lhs_z_1719 * i959_rhs_z_1723));
/*GENERATED*/                }
/*GENERATED*/            }
/*GENERATED*/            bool _731 = (((this.bodyLenHelp != 0) && (left != null)) && (right != null));
/*GENERATED*/            if (_731)  {
/*GENERATED*/                Vector3 i962_a = right.posAbs;
/*GENERATED*/                Vector3 i963_b = left.posAbs;
/*GENERATED*/                Vector3 newVar_704 = new Vector3((i962_a.x + -i963_b.x), 0, (i962_a.z + -i963_b.z));
/*GENERATED*/                Vector3 s0To1Proj = Vector3.Normalize(newVar_704);
/*GENERATED*/                float i967_THIS_y_1736 = wantedRot.y;
/*GENERATED*/                float i967_THIS_z_1737 = wantedRot.z;
/*GENERATED*/                float i972_y = (i967_THIS_y_1736 * 2);
/*GENERATED*/                float i973_z = (i967_THIS_z_1737 * 2);
/*GENERATED*/                Vector3 newVar_706 = new Vector3(
/*GENERATED*/                    (1 + -((i967_THIS_y_1736 * i972_y) + (i967_THIS_z_1737 * i973_z))), 
/*GENERATED*/                    0, 
/*GENERATED*/                    ((wantedRot.x * i973_z) + -(wantedRot.w * i972_y)));
/*GENERATED*/                Vector3 curLook = Vector3.Normalize(newVar_706);
/*GENERATED*/                float speedDump;
/*GENERATED*/                bool _741 = this.bodyLenHelpAtSpeedOnly;
/*GENERATED*/                if (_741)  {
/*GENERATED*/                    Vector3 i1000_a = this.inputWantedPos;
/*GENERATED*/                    Vector3 i1001_b = this.realBodyPos;
/*GENERATED*/                    float newVar_1335 = (i1000_a.x + -i1001_b.x);
/*GENERATED*/                    float newVar_1336 = (i1000_a.y + -i1001_b.y);
/*GENERATED*/                    float newVar_1337 = (i1000_a.z + -i1001_b.z);
/*GENERATED*/                    float i993_value = (
/*GENERATED*/                        (float)(Math.Sqrt(((newVar_1335 * newVar_1335) + (newVar_1336 * newVar_1336) + (newVar_1337 * newVar_1337)))) / 
/*GENERATED*/                        this.bodyLenHelpMaxSpeed);
/*GENERATED*/                    float i1004_arg1;
/*GENERATED*/                    bool _1442 = (i993_value < 1);
/*GENERATED*/                    if (_1442)  {
/*GENERATED*/                        i1004_arg1 = i993_value;
/*GENERATED*/                    } else  {
/*GENERATED*/                        i1004_arg1 = 1;
/*GENERATED*/                    }
/*GENERATED*/                    speedDump = (((0 > i1004_arg1)) ? (0) : (i1004_arg1));
/*GENERATED*/                } else  {
/*GENERATED*/                    speedDump = 1;
/*GENERATED*/                }
/*GENERATED*/                Quaternion newVar_710 = Quaternion.AngleAxis(
/*GENERATED*/                    (float)((
/*GENERATED*/                    (
/*GENERATED*/                    (
/*GENERATED*/                    ((curLook.x * s0To1Proj.x) + (curLook.y * s0To1Proj.y) + (curLook.z * s0To1Proj.z)) * 
/*GENERATED*/                    this.bodyLenHelp * 
/*GENERATED*/                    speedDump) / 
/*GENERATED*/                    Math.PI) * 
/*GENERATED*/                    180)), 
/*GENERATED*/                    this.up);
/*GENERATED*/                float i991_lhs_x_1745 = wantedRot.x;
/*GENERATED*/                float i991_lhs_y_1746 = wantedRot.y;
/*GENERATED*/                float i991_lhs_z_1747 = wantedRot.z;
/*GENERATED*/                float i991_lhs_w_1748 = wantedRot.w;
/*GENERATED*/                wantedRot.x = (
/*GENERATED*/                    (i991_lhs_w_1748 * newVar_710.x) + 
/*GENERATED*/                    (i991_lhs_x_1745 * newVar_710.w) + 
/*GENERATED*/                    (i991_lhs_y_1746 * newVar_710.z) + 
/*GENERATED*/                    -(i991_lhs_z_1747 * newVar_710.y));
/*GENERATED*/                wantedRot.y = (
/*GENERATED*/                    (i991_lhs_w_1748 * newVar_710.y) + 
/*GENERATED*/                    (i991_lhs_y_1746 * newVar_710.w) + 
/*GENERATED*/                    (i991_lhs_z_1747 * newVar_710.x) + 
/*GENERATED*/                    -(i991_lhs_x_1745 * newVar_710.z));
/*GENERATED*/                wantedRot.z = (
/*GENERATED*/                    (i991_lhs_w_1748 * newVar_710.z) + 
/*GENERATED*/                    (i991_lhs_z_1747 * newVar_710.w) + 
/*GENERATED*/                    (i991_lhs_x_1745 * newVar_710.y) + 
/*GENERATED*/                    -(i991_lhs_y_1746 * newVar_710.x));
/*GENERATED*/                wantedRot.w = (
/*GENERATED*/                    (i991_lhs_w_1748 * newVar_710.w) + 
/*GENERATED*/                    -(i991_lhs_x_1745 * newVar_710.x) + 
/*GENERATED*/                    -(i991_lhs_y_1746 * newVar_710.y) + 
/*GENERATED*/                    -(i991_lhs_z_1747 * newVar_710.z));
/*GENERATED*/            }
/*GENERATED*/            float baseY = 0;
/*GENERATED*/            for (int i = 0; (i < this.steps.Count); (i)++) baseY = (baseY + this.steps[i].bestTargetConservativeAbs.y);
/*GENERATED*/            float baseY_724 = (baseY / this.steps.Count);
/*GENERATED*/            float dockedDeviation = 0;
/*GENERATED*/            float airProgress = 0;
/*GENERATED*/            for (int i = 0; (i < this.steps.Count); (i)++)  {
/*GENERATED*/                Step2 step = this.steps[i];
/*GENERATED*/                bool _742 = !step.dockedState;
/*GENERATED*/                if (_742)  {
/*GENERATED*/                    airProgress = (airProgress + step.progress);
/*GENERATED*/                } else  {
/*GENERATED*/                     {
/*GENERATED*/                        float dd = (step.deviation / step.comfortRadius);
/*GENERATED*/                        bool _743 = (dd > dockedDeviation);
/*GENERATED*/                        if (_743)  {
/*GENERATED*/                            dockedDeviation = dd;
/*GENERATED*/                        }
/*GENERATED*/                    }
/*GENERATED*/                }
/*GENERATED*/            }
/*GENERATED*/            float i779_dstTo = this.downOnStep;
/*GENERATED*/            float i780_res = ((((dockedDeviation + (airProgress * 0.5f)) / 1) * (i779_dstTo + -1)) + 1);
/*GENERATED*/            float d;
/*GENERATED*/            bool _1440 = (1 < i779_dstTo);
/*GENERATED*/            if (_1440)  {
/*GENERATED*/                float i1576_arg1;
/*GENERATED*/                bool _1769 = (i780_res < i779_dstTo);
/*GENERATED*/                if (_1769)  {
/*GENERATED*/                    i1576_arg1 = i780_res;
/*GENERATED*/                } else  {
/*GENERATED*/                    i1576_arg1 = i779_dstTo;
/*GENERATED*/                }
/*GENERATED*/                d = (((1 > i1576_arg1)) ? (1) : (i1576_arg1));
/*GENERATED*/            } else  {
/*GENERATED*/                float i1583_arg1;
/*GENERATED*/                bool _1770 = (i780_res < 1);
/*GENERATED*/                if (_1770)  {
/*GENERATED*/                    i1583_arg1 = i780_res;
/*GENERATED*/                } else  {
/*GENERATED*/                    i1583_arg1 = 1;
/*GENERATED*/                }
/*GENERATED*/                d = (((i779_dstTo > i1583_arg1)) ? (i779_dstTo) : (i1583_arg1));
/*GENERATED*/            }
/*GENERATED*/            float i787_to = this.inputWantedPos.y;
/*GENERATED*/            float i788_progress = d;
/*GENERATED*/            Vector3 i789_a = this.inputWantedPos;
/*GENERATED*/            Vector3 i791_a = this.realBodyPos;
/*GENERATED*/            Vector3 newVar_658 = new Vector3((i789_a.x + -i791_a.x), 0, (i789_a.z + -i791_a.z));
/*GENERATED*/            Vector3 newVar_661 = new Vector3(additionalSpeed_x, 0, additionalSpeed_z);
/*GENERATED*/            this.realWantedSpeed = newVar_658.mul(this.horizontalMotor.distanceToSpeed).limit(this.horizontalMotor.maxSpeed).add(newVar_661);
/*GENERATED*/            float futureHeightDif;
/*GENERATED*/            bool _732 = ((this.runJumpTime > 0) && (dockedCount == 1));
/*GENERATED*/            if (_732)  {
/*GENERATED*/                float newVar_1346 = (this.realSpeed.y * this.runJumpTime);
/*GENERATED*/                float newVar_1349 = (this.g.y * this.runJumpTime);
/*GENERATED*/                futureHeightDif = (newVar_1346 + ((newVar_1349 * this.runJumpTime) / 2));
/*GENERATED*/            } else  {
/*GENERATED*/                futureHeightDif = 0;
/*GENERATED*/            }
/*GENERATED*/            float newVar_662 = (this.realBodyPos.y + futureHeightDif);
/*GENERATED*/            float newVar_663 = -this.g.y;
/*GENERATED*/            float verticalAccel = this.verticalMotor.getAccel(
/*GENERATED*/                ((baseY_724 * (1 + -i788_progress)) + (i787_to * i788_progress)), 
/*GENERATED*/                newVar_662, 
/*GENERATED*/                this.realSpeed.y, 
/*GENERATED*/                newVar_663, 
/*GENERATED*/                dockedCount);
/*GENERATED*/            Vector3 i799_a = this.realSpeed;
/*GENERATED*/            Vector3 i801_a = this.realWantedSpeed;
/*GENERATED*/            Vector3 newVar_664 = new Vector3((i801_a.x + -i799_a.x), i801_a.y, (i801_a.z + -i799_a.z));
/*GENERATED*/            Vector3 realAccel = newVar_664.mul(this.horizontalMotor.speedDifToAccel).limit((this.horizontalMotor.maxAccel * dockedCount)).add(0, verticalAccel, 0);
/*GENERATED*/            Vector3 oldImCenterPos = this.imCenter;
/*GENERATED*/            Vector3 i805_a = this.inputWantedPos;
/*GENERATED*/            Vector3 i807_a = this.imCenter;
/*GENERATED*/            Vector3 newVar_666 = new Vector3((i805_a.x + -i807_a.x), 0, (i805_a.z + -i807_a.z));
/*GENERATED*/            Vector3 imCenterWantedSpeed = newVar_666.mul(this.horizontalMotor.distanceToSpeed).limit(this.horizontalMotor.maxSpeed);
/*GENERATED*/            Vector3 i813_a = this.imCenterSpeed;
/*GENERATED*/            Vector3 newVar_669 = new Vector3((imCenterWantedSpeed.x + -i813_a.x), imCenterWantedSpeed.y, (imCenterWantedSpeed.z + -i813_a.z));
/*GENERATED*/            Vector3 imCenterAccel = newVar_669.mul(this.horizontalMotor.speedDifToAccel);
/*GENERATED*/            Vector3 imCenterAccel_725 = imCenterAccel.limit((this.horizontalMotor.maxAccel * dockedCount));
/*GENERATED*/            Vector3 i821_a = this.imCenterSpeed;
/*GENERATED*/            this.imCenterSpeed.x = (i821_a.x + (imCenterAccel_725.x * dt));
/*GENERATED*/            this.imCenterSpeed.y = (i821_a.y + (imCenterAccel_725.y * dt));
/*GENERATED*/            this.imCenterSpeed.z = (i821_a.z + (imCenterAccel_725.z * dt));
/*GENERATED*/            this.imCenterSpeed.y = this.realSpeed.y;
/*GENERATED*/            this.imCenter.y = this.realBodyPos.y;
/*GENERATED*/            Vector3 i823_a = this.imCenterSpeed;
/*GENERATED*/            Vector3 i825_a = this.imCenter;
/*GENERATED*/            this.imCenter.x = (i825_a.x + (i823_a.x * dt));
/*GENERATED*/            this.imCenter.y = (i825_a.y + (i823_a.y * dt));
/*GENERATED*/            this.imCenter.z = (i825_a.z + (i823_a.z * dt));
/*GENERATED*/            Vector3 i827_a = this.imCenter;
/*GENERATED*/            Vector3 i828_b = this.imBody;
/*GENERATED*/            this.imCenter.x = (((i828_b.x + -i827_a.x) * 0.05f) + i827_a.x);
/*GENERATED*/            this.imCenter.y = (((i828_b.y + -i827_a.y) * 0.05f) + i827_a.y);
/*GENERATED*/            this.imCenter.z = (((i828_b.z + -i827_a.z) * 0.05f) + i827_a.z);
/*GENERATED*/            Vector3 oldImBodyPos = this.imBody;
/*GENERATED*/            Vector3 i836_a = this.imBodySpeed;
/*GENERATED*/            Vector3 i838_a = this.realWantedSpeed;
/*GENERATED*/            Vector3 newVar_676 = new Vector3((i838_a.x + -i836_a.x), i838_a.y, (i838_a.z + -i836_a.z));
/*GENERATED*/            this.imBodySpeed = (
/*GENERATED*/                this.imBodySpeed + 
/*GENERATED*/                (
/*GENERATED*/                newVar_676.mul(this.horizontalMotor.speedDifToAccel).limit((this.horizontalMotor.maxAccel * dockedCount)) * 
/*GENERATED*/                dt));
/*GENERATED*/            Vector3 i842_a = this.imBodySpeed;
/*GENERATED*/            Vector3 i844_a = this.imBody;
/*GENERATED*/            this.imBody.x = (i844_a.x + (i842_a.x * dt));
/*GENERATED*/            this.imBody.y = (i844_a.y + (i842_a.y * dt));
/*GENERATED*/            this.imBody.z = (i844_a.z + (i842_a.z * dt));
/*GENERATED*/            Vector3 i846_a = this.realBodyPos;
/*GENERATED*/            Vector3 i847_b = this.imBody;
/*GENERATED*/            float newVar_1124 = ((i846_a.x + -i847_b.x) * 0.1f);
/*GENERATED*/            float newVar_1125 = ((i846_a.y + -i847_b.y) * 0.1f);
/*GENERATED*/            float newVar_1126 = ((i846_a.z + -i847_b.z) * 0.1f);
/*GENERATED*/            Vector3 i852_a = this.imBody;
/*GENERATED*/            this.imBody.x = (i852_a.x + newVar_1124);
/*GENERATED*/            this.imBody.y = (i852_a.y + newVar_1125);
/*GENERATED*/            this.imBody.z = (i852_a.z + newVar_1126);
/*GENERATED*/            Vector3 i854_a = this.imCenter;
/*GENERATED*/            this.imCenter.x = (i854_a.x + newVar_1124);
/*GENERATED*/            this.imCenter.y = (i854_a.y + newVar_1125);
/*GENERATED*/            this.imCenter.z = (i854_a.z + newVar_1126);
/*GENERATED*/            Vector3 i856_a = this.imBody;
/*GENERATED*/            this.imBodySpeed.x = ((i856_a.x + -oldImBodyPos.x) / dt);
/*GENERATED*/            this.imBodySpeed.y = ((i856_a.y + -oldImBodyPos.y) / dt);
/*GENERATED*/            this.imBodySpeed.z = ((i856_a.z + -oldImBodyPos.z) / dt);
/*GENERATED*/            Vector3 i864_a = this.imCenter;
/*GENERATED*/            this.imActualCenterSpeed.x = ((i864_a.x + -oldImCenterPos.x) / dt);
/*GENERATED*/            this.imActualCenterSpeed.y = ((i864_a.y + -oldImCenterPos.y) / dt);
/*GENERATED*/            this.imActualCenterSpeed.z = ((i864_a.z + -oldImCenterPos.z) / dt);
/*GENERATED*/            Vector3 i872_a = this.imBody;
/*GENERATED*/            Vector3 i873_b = this.realBodyPos;
/*GENERATED*/            float i876_diff_x = (i872_a.x + -i873_b.x);
/*GENERATED*/            float i877_diff_y = (i872_a.y + -i873_b.y);
/*GENERATED*/            float i878_diff_z = (i872_a.z + -i873_b.z);
/*GENERATED*/            bool _733 = (
/*GENERATED*/                (float)(Math.Sqrt(((i876_diff_x * i876_diff_x) + (i877_diff_y * i877_diff_y) + (i878_diff_z * i878_diff_z)))) > 
/*GENERATED*/                5);
/*GENERATED*/            if (_733)  {
/*GENERATED*/                this.imCenter = this.realBodyPos;
/*GENERATED*/                this.imBody = this.realBodyPos;
/*GENERATED*/                this.imCenterSpeed = this.realSpeed;
/*GENERATED*/                this.imBodySpeed = this.realSpeed;
/*GENERATED*/            }
/*GENERATED*/            Vector3 i879_a = this.imCenter;
/*GENERATED*/            Vector3 i880_b = this.imBody;
/*GENERATED*/            float i883_diff_x = (i879_a.x + -i880_b.x);
/*GENERATED*/            float i884_diff_y = (i879_a.y + -i880_b.y);
/*GENERATED*/            float i885_diff_z = (i879_a.z + -i880_b.z);
/*GENERATED*/            bool _734 = (
/*GENERATED*/                (float)(Math.Sqrt(((i883_diff_x * i883_diff_x) + (i884_diff_y * i884_diff_y) + (i885_diff_z * i885_diff_z)))) > 
/*GENERATED*/                (this.cogAccel * 0.3f));
/*GENERATED*/            if (_734)  {
/*GENERATED*/                this.imCenter = this.imBody;
/*GENERATED*/                this.imCenterSpeed = this.imBodySpeed;
/*GENERATED*/            }
/*GENERATED*/            Vector3 i886_a = this.inputWantedPos;
/*GENERATED*/            Vector3 i887_b = this.imCenter;
/*GENERATED*/            Vector3 newVar_687 = new Vector3((i886_a.x + -i887_b.x), (i886_a.y + -i887_b.y), (i886_a.z + -i887_b.z));
/*GENERATED*/            Vector3 speedDif = (this.imActualCenterSpeed - (newVar_687 * this.horizontalMotor.distanceToSpeed));
/*GENERATED*/            Vector3 speedDif_726 = speedDif.limit(this.horizontalMotor.maxSpeed);
/*GENERATED*/            Vector3 i888_a = new Vector3();
/*GENERATED*/            i888_a.x = additionalSpeed_x;
/*GENERATED*/            i888_a.y = additionalSpeed_y;
/*GENERATED*/            i888_a.z = additionalSpeed_z;
/*GENERATED*/            Vector3 addNormalized = Vector3.Normalize(i888_a);
/*GENERATED*/            float i889_a_x_1675 = additionalSpeed_x;
/*GENERATED*/            float i889_a_y_1676 = additionalSpeed_y;
/*GENERATED*/            float i889_a_z_1677 = additionalSpeed_z;
/*GENERATED*/            float adLen = (float)(Math.Sqrt(((i889_a_x_1675 * i889_a_x_1675) + (i889_a_y_1676 * i889_a_y_1676) + (i889_a_z_1677 * i889_a_z_1677))));
/*GENERATED*/            float proj = (
/*GENERATED*/                (speedDif_726.x * addNormalized.x) + 
/*GENERATED*/                (speedDif_726.y * addNormalized.y) + 
/*GENERATED*/                (speedDif_726.z * addNormalized.z));
/*GENERATED*/            bool _735 = (proj > adLen);
/*GENERATED*/            if (_735)  {
/*GENERATED*/                proj = (proj + -adLen);
/*GENERATED*/            } else  {
/*GENERATED*/                bool _744 = (proj < -adLen);
/*GENERATED*/                if (_744)  {
/*GENERATED*/                    proj = (proj + adLen);
/*GENERATED*/                } else  {
/*GENERATED*/                    proj = 0;
/*GENERATED*/                }
/*GENERATED*/            }
/*GENERATED*/            float i900_b = (
/*GENERATED*/                (addNormalized.x * speedDif_726.x) + 
/*GENERATED*/                (addNormalized.y * speedDif_726.y) + 
/*GENERATED*/                (addNormalized.z * speedDif_726.z));
/*GENERATED*/            float i908_b = proj;
/*GENERATED*/            Vector3 newVar_688 = new Vector3(
/*GENERATED*/                (speedDif_726.x + -(addNormalized.x * i900_b) + (addNormalized.x * i908_b)), 
/*GENERATED*/                (speedDif_726.y + -(addNormalized.y * i900_b) + (addNormalized.y * i908_b)), 
/*GENERATED*/                (speedDif_726.z + -(addNormalized.z * i900_b) + (addNormalized.z * i908_b)));
/*GENERATED*/            float newVar_693 = (this.midLen * this.lackOfSpeedCompensation);
/*GENERATED*/            Vector3 newVar_692;
/*GENERATED*/            bool _1441 = (
/*GENERATED*/                (float)(Math.Sqrt(((newVar_688.x * newVar_688.x) + (newVar_688.y * newVar_688.y) + (newVar_688.z * newVar_688.z)))) > 
/*GENERATED*/                newVar_693);
/*GENERATED*/            if (_1441)  {
/*GENERATED*/                Vector3 i1588_a = Vector3.Normalize(newVar_688);
/*GENERATED*/                Vector3 newVar_1590 = new Vector3((i1588_a.x * newVar_693), (i1588_a.y * newVar_693), (i1588_a.z * newVar_693));
/*GENERATED*/                newVar_692 = newVar_1590;
/*GENERATED*/            } else  {
/*GENERATED*/                newVar_692 = newVar_688;
/*GENERATED*/            }
/*GENERATED*/            Vector3 i920_a = this.speedLack;
/*GENERATED*/            this.speedLack.x = (((newVar_692.x + -i920_a.x) * 0.1f) + i920_a.x);
/*GENERATED*/            this.speedLack.y = (((newVar_692.y + -i920_a.y) * 0.1f) + i920_a.y);
/*GENERATED*/            this.speedLack.z = (((newVar_692.z + -i920_a.z) * 0.1f) + i920_a.z);
/*GENERATED*/            this.speedLack.y = 0;
/*GENERATED*/            Vector3 i929_a = this.imCenter;
/*GENERATED*/            Vector3 i930_b = this.speedLack;
/*GENERATED*/            this.virtualForLegs.x = (i929_a.x + i930_b.x);
/*GENERATED*/            this.virtualForLegs.y = (i929_a.y + i930_b.y);
/*GENERATED*/            this.virtualForLegs.z = (i929_a.z + i930_b.z);
/*GENERATED*/            bool _736 = (realAccel.y > 0);
/*GENERATED*/            if (_736)  {
/*GENERATED*/                this.resultAcceleration = realAccel;
/*GENERATED*/            } else  {
/*GENERATED*/                this.resultAcceleration.x = 0;
/*GENERATED*/                this.resultAcceleration.y = 0;
/*GENERATED*/                this.resultAcceleration.z = 0;
/*GENERATED*/            }
/*GENERATED*/            this.resultRotAcceleration = this.rotationMotor.getTorque(this.realBodyRot, wantedRot, this.realBodyAngularSpeed, ((float)(dockedCount) / this.steps.Count));
/*GENERATED*/            bool _737 = this.doTickHip;
/*GENERATED*/            if (_737)  {
/*GENERATED*/                bool _745 = ((left != null) && (right != null));
/*GENERATED*/                if (_745)  {
/*GENERATED*/                    this.calcHipLenHelp();
/*GENERATED*/                }
/*GENERATED*/                Quaternion i1007_THIS = this.wantedHipRot;
/*GENERATED*/                Quaternion i1008_from = this.realBodyRot;
/*GENERATED*/                float newVar_1351 = -i1008_from.x;
/*GENERATED*/                float newVar_1352 = -i1008_from.y;
/*GENERATED*/                float newVar_1353 = -i1008_from.z;
/*GENERATED*/                float i1457_w = i1008_from.w;
/*GENERATED*/                Quaternion newVar_719 = new Quaternion((
/*GENERATED*/                    (i1007_THIS.w * newVar_1351) + 
/*GENERATED*/                    (i1007_THIS.x * i1457_w) + 
/*GENERATED*/                    (i1007_THIS.y * newVar_1353) + 
/*GENERATED*/                    -(i1007_THIS.z * newVar_1352)), (
/*GENERATED*/                    (i1007_THIS.w * newVar_1352) + 
/*GENERATED*/                    (i1007_THIS.y * i1457_w) + 
/*GENERATED*/                    (i1007_THIS.z * newVar_1351) + 
/*GENERATED*/                    -(i1007_THIS.x * newVar_1353)), (
/*GENERATED*/                    (i1007_THIS.w * newVar_1353) + 
/*GENERATED*/                    (i1007_THIS.z * i1457_w) + 
/*GENERATED*/                    (i1007_THIS.x * newVar_1352) + 
/*GENERATED*/                    -(i1007_THIS.y * newVar_1351)), (
/*GENERATED*/                    (i1007_THIS.w * i1457_w) + 
/*GENERATED*/                    -(i1007_THIS.x * newVar_1351) + 
/*GENERATED*/                    -(i1007_THIS.y * newVar_1352) + 
/*GENERATED*/                    -(i1007_THIS.z * newVar_1353)));
/*GENERATED*/                Quaternion newVar_718 = Quaternion.Lerp(this.slowLocalHipRot, newVar_719, 0.1f);
/*GENERATED*/                this.slowLocalHipRot = newVar_718;
/*GENERATED*/                Quaternion i1017_THIS = this.realBodyRot;
/*GENERATED*/                Vector3 i1018_vector = this.hipPosRel;
/*GENERATED*/                float i1021_x = (i1017_THIS.x * 2);
/*GENERATED*/                float i1022_y = (i1017_THIS.y * 2);
/*GENERATED*/                float i1023_z = (i1017_THIS.z * 2);
/*GENERATED*/                float i1024_xx = (i1017_THIS.x * i1021_x);
/*GENERATED*/                float i1025_yy = (i1017_THIS.y * i1022_y);
/*GENERATED*/                float i1026_zz = (i1017_THIS.z * i1023_z);
/*GENERATED*/                float i1027_xy = (i1017_THIS.x * i1022_y);
/*GENERATED*/                float i1028_xz = (i1017_THIS.x * i1023_z);
/*GENERATED*/                float i1029_yz = (i1017_THIS.y * i1023_z);
/*GENERATED*/                float i1030_wx = (i1017_THIS.w * i1021_x);
/*GENERATED*/                float i1031_wy = (i1017_THIS.w * i1022_y);
/*GENERATED*/                float i1032_wz = (i1017_THIS.w * i1023_z);
/*GENERATED*/                Vector3 i1035_b = this.realBodyPos;
/*GENERATED*/                this.hipPosAbs.x = (
/*GENERATED*/                    ((1 + -(i1025_yy + i1026_zz)) * i1018_vector.x) + 
/*GENERATED*/                    ((i1027_xy + -i1032_wz) * i1018_vector.y) + 
/*GENERATED*/                    ((i1028_xz + i1031_wy) * i1018_vector.z) + 
/*GENERATED*/                    i1035_b.x);
/*GENERATED*/                this.hipPosAbs.y = (
/*GENERATED*/                    ((i1027_xy + i1032_wz) * i1018_vector.x) + 
/*GENERATED*/                    ((1 + -(i1024_xx + i1026_zz)) * i1018_vector.y) + 
/*GENERATED*/                    ((i1029_yz + -i1030_wx) * i1018_vector.z) + 
/*GENERATED*/                    i1035_b.y);
/*GENERATED*/                this.hipPosAbs.z = (
/*GENERATED*/                    ((i1028_xz + -i1031_wy) * i1018_vector.x) + 
/*GENERATED*/                    ((i1029_yz + i1030_wx) * i1018_vector.y) + 
/*GENERATED*/                    ((1 + -(i1024_xx + i1025_yy)) * i1018_vector.z) + 
/*GENERATED*/                    i1035_b.z);
/*GENERATED*/                bool _746 = (this.hipFlexibility == 0);
/*GENERATED*/                if (_746)  {
/*GENERATED*/                    this.hipRotAbs = this.realBodyRot;
/*GENERATED*/                } else  {
/*GENERATED*/                    Quaternion i1036_lhs = this.slowLocalHipRot;
/*GENERATED*/                    Quaternion i1037_rhs = this.realBodyRot;
/*GENERATED*/                    Quaternion newVar_723 = new Quaternion((
/*GENERATED*/                        (i1036_lhs.w * i1037_rhs.x) + 
/*GENERATED*/                        (i1036_lhs.x * i1037_rhs.w) + 
/*GENERATED*/                        (i1036_lhs.y * i1037_rhs.z) + 
/*GENERATED*/                        -(i1036_lhs.z * i1037_rhs.y)), (
/*GENERATED*/                        (i1036_lhs.w * i1037_rhs.y) + 
/*GENERATED*/                        (i1036_lhs.y * i1037_rhs.w) + 
/*GENERATED*/                        (i1036_lhs.z * i1037_rhs.x) + 
/*GENERATED*/                        -(i1036_lhs.x * i1037_rhs.z)), (
/*GENERATED*/                        (i1036_lhs.w * i1037_rhs.z) + 
/*GENERATED*/                        (i1036_lhs.z * i1037_rhs.w) + 
/*GENERATED*/                        (i1036_lhs.x * i1037_rhs.y) + 
/*GENERATED*/                        -(i1036_lhs.y * i1037_rhs.x)), (
/*GENERATED*/                        (i1036_lhs.w * i1037_rhs.w) + 
/*GENERATED*/                        -(i1036_lhs.x * i1037_rhs.x) + 
/*GENERATED*/                        -(i1036_lhs.y * i1037_rhs.y) + 
/*GENERATED*/                        -(i1036_lhs.z * i1037_rhs.z)));
/*GENERATED*/                    Quaternion newVar_722 = Quaternion.Lerp(this.realBodyRot, newVar_723, this.hipFlexibility);
/*GENERATED*/                    this.hipRotAbs = newVar_722;
/*GENERATED*/                }
/*GENERATED*/            }
/*GENERATED*/        }

        Quaternion oldHipLenHelp = Quaternion.identity;

/*GENERATED*/        [Optimize]
/*GENERATED*/        void calcHipLenHelp() {
/*GENERATED*/            bool _1823 = (this.steps.Count < 2);
/*GENERATED*/            if (_1823)  {
/*GENERATED*/                return ;
/*GENERATED*/            }
/*GENERATED*/            Step2 left = this.steps[this.leadingLegLeft];
/*GENERATED*/            Step2 right = this.steps[this.leadingLegRight];
/*GENERATED*/            Vector3 leg1 = this.surfaceDetector.detect(left.posAbs, Vector3.up);
/*GENERATED*/            Vector3 leg2 = this.surfaceDetector.detect(right.posAbs, Vector3.up);
/*GENERATED*/            Vector3 newVar_1805 = this.surfaceDetector.detect(left.basisAbs, Vector3.up);
/*GENERATED*/            Vector3 i1830_b = left.posAbs;
/*GENERATED*/            float i1833_diff_x = (newVar_1805.x + -i1830_b.x);
/*GENERATED*/            float i1834_diff_y = (newVar_1805.y + -i1830_b.y);
/*GENERATED*/            float i1835_diff_z = (newVar_1805.z + -i1830_b.z);
/*GENERATED*/            float d1 = (
/*GENERATED*/                (float)(Math.Sqrt(((i1833_diff_x * i1833_diff_x) + (i1834_diff_y * i1834_diff_y) + (i1835_diff_z * i1835_diff_z)))) + 
/*GENERATED*/                (0.1f * this.midLen));
/*GENERATED*/            Vector3 newVar_1808 = this.surfaceDetector.detect(right.basisAbs, Vector3.up);
/*GENERATED*/            Vector3 i1838_b = right.posAbs;
/*GENERATED*/            float i1841_diff_x = (newVar_1808.x + -i1838_b.x);
/*GENERATED*/            float i1842_diff_y = (newVar_1808.y + -i1838_b.y);
/*GENERATED*/            float i1843_diff_z = (newVar_1808.z + -i1838_b.z);
/*GENERATED*/            float d2 = (
/*GENERATED*/                (float)(Math.Sqrt(((i1841_diff_x * i1841_diff_x) + (i1842_diff_y * i1842_diff_y) + (i1843_diff_z * i1843_diff_z)))) + 
/*GENERATED*/                (0.1f * this.midLen));
/*GENERATED*/            bool _1824 = !left.dockedState;
/*GENERATED*/            if (_1824)  {
/*GENERATED*/                d1 = (d1 + -this.midLen);
/*GENERATED*/            }
/*GENERATED*/            bool _1825 = !right.dockedState;
/*GENERATED*/            if (_1825)  {
/*GENERATED*/                d2 = (d2 + -this.midLen);
/*GENERATED*/            }
/*GENERATED*/            float forPare_1957 = (d1 + d2);
/*GENERATED*/            float newVar_1812 = (1 + -(d1 / forPare_1957));
/*GENERATED*/            Vector3 i1844_a = this.up;
/*GENERATED*/            float newVar_1811_x = 0;
/*GENERATED*/            float newVar_1811_y = 0;
/*GENERATED*/            float newVar_1811_z = 0;
/*GENERATED*/            newVar_1811_x = (i1844_a.x * newVar_1812);
/*GENERATED*/            newVar_1811_y = (i1844_a.y * newVar_1812);
/*GENERATED*/            newVar_1811_z = (i1844_a.z * newVar_1812);
/*GENERATED*/            float newVar_1810_x = 0;
/*GENERATED*/            float newVar_1810_y = 0;
/*GENERATED*/            float newVar_1810_z = 0;
/*GENERATED*/            newVar_1810_x = newVar_1811_x;
/*GENERATED*/            newVar_1810_y = newVar_1811_y;
/*GENERATED*/            newVar_1810_z = newVar_1811_z;
/*GENERATED*/            float dbgPoint1_x = 0;
/*GENERATED*/            float dbgPoint1_y = 0;
/*GENERATED*/            float dbgPoint1_z = 0;
/*GENERATED*/            dbgPoint1_x = (leg1.x + newVar_1810_x);
/*GENERATED*/            dbgPoint1_y = (leg1.y + newVar_1810_y);
/*GENERATED*/            dbgPoint1_z = (leg1.z + newVar_1810_z);
/*GENERATED*/            float newVar_1817 = (1 + -(d2 / forPare_1957));
/*GENERATED*/            Vector3 i1850_a = this.up;
/*GENERATED*/            float newVar_1816_x = 0;
/*GENERATED*/            float newVar_1816_y = 0;
/*GENERATED*/            float newVar_1816_z = 0;
/*GENERATED*/            newVar_1816_x = (i1850_a.x * newVar_1817);
/*GENERATED*/            newVar_1816_y = (i1850_a.y * newVar_1817);
/*GENERATED*/            newVar_1816_z = (i1850_a.z * newVar_1817);
/*GENERATED*/            float newVar_1815_x = 0;
/*GENERATED*/            float newVar_1815_y = 0;
/*GENERATED*/            float newVar_1815_z = 0;
/*GENERATED*/            newVar_1815_x = newVar_1816_x;
/*GENERATED*/            newVar_1815_y = newVar_1816_y;
/*GENERATED*/            newVar_1815_z = newVar_1816_z;
/*GENERATED*/            float dbgPoint2_x = 0;
/*GENERATED*/            float dbgPoint2_y = 0;
/*GENERATED*/            float dbgPoint2_z = 0;
/*GENERATED*/            dbgPoint2_x = (leg2.x + newVar_1815_x);
/*GENERATED*/            dbgPoint2_y = (leg2.y + newVar_1815_y);
/*GENERATED*/            dbgPoint2_z = (leg2.z + newVar_1815_z);
/*GENERATED*/            Vector3 Z = new Vector3((dbgPoint1_x + -dbgPoint2_x), (dbgPoint1_y + -dbgPoint2_y), (dbgPoint1_z + -dbgPoint2_z));
/*GENERATED*/            Vector3 i1859_Y = this.up;
/*GENERATED*/            Vector3 newVar_1909 = Vector3.Normalize(Z);
/*GENERATED*/            Vector3 i1862_a = new Vector3(
/*GENERATED*/                ((i1859_Y.y * newVar_1909.z) + -(i1859_Y.z * newVar_1909.y)), 
/*GENERATED*/                ((i1859_Y.z * newVar_1909.x) + -(i1859_Y.x * newVar_1909.z)), 
/*GENERATED*/                ((i1859_Y.x * newVar_1909.y) + -(i1859_Y.y * newVar_1909.x)));
/*GENERATED*/            Vector3 i1860_X = Vector3.Normalize(i1862_a);
/*GENERATED*/            Vector3 i1867_a = new Vector3(
/*GENERATED*/                ((newVar_1909.y * i1860_X.z) + -(newVar_1909.z * i1860_X.y)), 
/*GENERATED*/                ((newVar_1909.z * i1860_X.x) + -(newVar_1909.x * i1860_X.z)), 
/*GENERATED*/                ((newVar_1909.x * i1860_X.y) + -(newVar_1909.y * i1860_X.x)));
/*GENERATED*/            Vector3 newVar_1928 = Vector3.Normalize(i1867_a);
/*GENERATED*/            Quaternion newVar_1821 = MUtil.qToAxes(
/*GENERATED*/                i1860_X.x, 
/*GENERATED*/                i1860_X.y, 
/*GENERATED*/                i1860_X.z, 
/*GENERATED*/                newVar_1928.x, 
/*GENERATED*/                newVar_1928.y, 
/*GENERATED*/                newVar_1928.z, 
/*GENERATED*/                newVar_1909.x, 
/*GENERATED*/                newVar_1909.y, 
/*GENERATED*/                newVar_1909.z);
/*GENERATED*/            Quaternion newVar_1820 = Quaternion.Lerp(this.oldHipLenHelp, newVar_1821, 0.2f);
/*GENERATED*/            this.oldHipLenHelp = newVar_1820;
/*GENERATED*/            Quaternion newVar_1822 = Quaternion.Lerp(this.wantedHipRot, this.oldHipLenHelp, 0.8f);
/*GENERATED*/            this.wantedHipRot = newVar_1822;
/*GENERATED*/        }

        [HideInInspector] public float emphasis;
        public Vector3 realWantedSpeed;

        public virtual void tickSteps(float dt) {
            Step2 right = (((this.leadingLegRight < this.steps.Count)) ? (this.steps[this.leadingLegRight]) : (null));
            Step2 left = (((this.leadingLegLeft < this.steps.Count)) ? (this.steps[this.leadingLegLeft]) : (null));
            if ((right != null))  {
                right.additionalDisplacement = new Vector3(-this.emphasis, 0, 0);
            }
            for (int stepIndex = 0; (stepIndex < this.steps.Count); (stepIndex)++)  {
                Step2 step = this.steps[stepIndex];
                float hDif = MyMath.clamp(((step.bestTargetProgressiveAbs.y - (step.posAbs.y + step.lastDockedAtLocal.y)) / step.maxLen), -1, 1);
                step.undockHDif = (1 + Math.Max(-0.8f, (((hDif > 0)) ? ((hDif * 5)) : (hDif))));
                 {
                    float value = (-1 + Math.Min(1, step.landTime));
                    step.fromAbove += value;
                    if (this.collectSteppingHistory)  {
                        step.paramHistory.setValue(HistoryInfoBean.lt, value);
                    }
                }
                float restTime = 0.5f;
                if ((step.dockedState && (step.landTime < restTime)))  {
                    float value = ((-1 + (step.landTime / restTime)) * 5);
                    step.fromAbove += value;
                    if (this.collectSteppingHistory)  {
                        step.paramHistory.setValue(HistoryInfoBean.lt2, value);
                    }
                }
                if (!step.dockedState)  {
                    for (int index = 0; (index < step.affectedByProgress.Count); (index)++)  {
                        StepNeuro<Step2> affected = step.affectedByProgress[index];
                        float value = (affected.add + (affected.mul * step.progress));
                        affected.leg.fromAbove += value;
                        if (this.collectSteppingHistory)  {
                            affected.leg.paramHistory.setValue(affected.desc, value);
                        }
                    }
                } else  {
                     {
                        for (int index = 0; (index < step.affectedByDeviation.Count); (index)++)  {
                            StepNeuro<Step2> affected = step.affectedByDeviation[index];
                            float value = (affected.add + (affected.mul * step.deviation));
                            affected.leg.fromAbove += value;
                            if (this.collectSteppingHistory)  {
                                affected.leg.paramHistory.setValue(affected.desc, value);
                            }
                        }
                    }
                }
            }
            if (((left != null) && (right != null)))  {
                left.forbidHalf = this.bipedalForbidPlacement;
                right.forbidHalf = this.bipedalForbidPlacement;
                if (this.bipedalForbidPlacement)  {
                    left.forbidHalfPos = right.posAbs;
                    right.forbidHalfPos = left.posAbs;
                    left.forbidHalfDir = ExtensionMethods.normalized(ExtensionMethods.withSetY(ExtensionMethods.sub(left.basisAbs, right.basisAbs), 0));
                    right.forbidHalfDir = -left.forbidHalfDir;
                }
                if (((this.forceBipedalEarlyStep && right.dockedState) && left.dockedState))  {
                    Vector3 zeroSpeed = ExtensionMethods.withSetY(this.imCenterSpeed, 0);
                    if ((ExtensionMethods.length(zeroSpeed) > (0.2f * this.midLen)))  {
                        float sRight = ExtensionMethods.scalarProduct(ExtensionMethods.sub(right.posAbs, this.realBodyPos), zeroSpeed);
                        float sLeft = ExtensionMethods.scalarProduct(ExtensionMethods.sub(left.posAbs, this.realBodyPos), zeroSpeed);
                        if (((sRight < 0) && (sLeft < 0)))  {
                            left.fromAbove -= (sLeft / this.midLen);
                            if (this.collectSteppingHistory)  {
                                left.paramHistory.setValue(HistoryInfoBean.bipedEarlyStep, (-sLeft / this.midLen));
                            }
                            right.fromAbove -= (sRight / this.midLen);
                            if (this.collectSteppingHistory)  {
                                right.paramHistory.setValue(HistoryInfoBean.bipedEarlyStep, (-sRight / this.midLen));
                            }
                        }
                    }
                }
            }
            if (this.collectSteppingHistory)  {
                foreach (Step2 step in this.steps) {
                    step.paramHistory.setValue(HistoryInfoBean.fromAbove, step.fromAbove);
                }
            }
            float biggestFromAbove = 0;
            Step2 biggestStep = null;
            for (int i = 0; (i < this.steps.Count); (i)++)  {
                Step2 step = this.steps[i];
                if ((step.dockedState && (step.fromAbove > biggestFromAbove)))  {
                    biggestFromAbove = step.fromAbove;
                    biggestStep = step;
                }
            }
            if ((biggestStep != null))  {
                biggestStep.beginStep(1);
            }
            for (int i = 0; (i < this.steps.Count); (i)++)  {
                MoveenSkelBase skel = this.legSkel[i];
                Step2 step = this.steps[i];
                step.tick(dt);
                if ((skel != null))  {
                    skel.setTarget(step.posAbs, step.footOrientation);
                    skel.tick(dt);
                    step.comfortFromSkel = skel.comfort;
                    step.posAbs = skel.limitedResultTarget;
                } else  {
                     {
                    }
                }
            }
        }
        void calcAbs(float dt) {
            float bodySpeedForLeg = Math.Min(
                this.horizontalMotor.maxSpeed, 
                Math.Max(ExtensionMethods.length(this.realSpeed), ExtensionMethods.length((this.realBodyPos - this.inputWantedPos))));
            for (int i = 0; (i < this.steps.Count); (i)++)  {
                Step2 step = this.steps[i];
                step.g = this.g;
                step.basisAbs = step.thisTransform.position;
                step.projectedRot = this.projectedRot;
                step.legSpeed = MyMath.max(
                    0, 
                    step.stepSpeedMin, 
                    (bodySpeedForLeg * step.stepSpeedBodySpeedMul), 
                    (ExtensionMethods.length(this.realBodyAngularSpeed) * step.stepSpeedBodyRotSpeedMul));
                step.bodyPos = this.imCenter;
                step.bodyRot = this.realBodyRot;
                step.bodySpeed = this.imActualCenterSpeed;
                step.calcAbs(dt, this.virtualForLegs, this.inputWantedRot);
                step.fromAbove = -1;
            }
        }
        public virtual void reset(Vector3 pos, Quaternion rot) {
            this.inputWantedPos = pos;
            this.inputWantedRot = rot;
            this.realBodyPos = pos;
            this.realBodyRot = rot;
            this.hipPosAbs = (ExtensionMethods.rotate(this.realBodyRot, this.hipPosRel) + this.realBodyPos);
            this.hipRotAbs = this.realBodyRot;
            this.imCenter = this.realBodyPos;
            this.imBody = this.realBodyPos;
            for (int index = 0; (index < this.steps.Count); (index)++)  {
                this.steps[index].reset(pos, rot);
            }
        }
        public virtual Vector3 project(Vector3 input) {
            return this.surfaceDetector.detect(input, Vector3.up);
        }

    }
}
