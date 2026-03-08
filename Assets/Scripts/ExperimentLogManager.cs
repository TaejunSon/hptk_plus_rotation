using System;
using UnityEngine;

public class ExperimentLogManager : MonoBehaviour
{   
    private EvaluationSceneManager_HPTK_2 _sm;
    private const string EVENT_GRAB = "Grab";
    private const string EVENT_RELEASE = "Release";
    private const string EVENT_GRAB_LEFT = "Grab_left";
    private const string EVENT_RELEASE_LEFT = "Release_left";

    private const string BASE_DIRECTORY_PATH = @"C:\Users\Taejun\hptk_plus_rotation\Data\";
    private const string EXPERIMENT_LABEL = "Evaluation_Exp2";
    //private const string RIGHT_PUPPET_PATH = "DefaultAvatar.AB.URP/Representations/Hand.R/Puppet.AB";
    //private const string RIGHT_PUPPET_PATH_CLONE = "DefaultAvatar.AB.URP (Clone)/Representations/Hand.R/Puppet.AB";

    private CsvLog _streamLog = new CsvLog();
    private CsvLog _eventLog = new CsvLog();
    private CsvLog _summaryLog = new CsvLog();

    private string _basePath;
    private string _conditionsSuffix;
    private long _timestamp;
    private string _eventName;
    private int _eventHandIndex = -1; // -1: N/A, 0: Right, 1: Left
    private float _taskCompletionTime;

    // Accumulation fields for summary
    private Vector3 _prevThumbTipLocal, _prevIndexTipLocal, _prevMiddleTipLocal;
    private Vector3 _prevThumbTipLocal_left, _prevIndexTipLocal_left, _prevMiddleTipLocal_left;
    private Vector3 _prevWristWorldPos, _prevDieWorldPos, _prevHeadWorldPos;
    private Vector3 _prevWristWorldPos_left;
    private Quaternion _prevWristWorldRot, _prevDieWorldRot, _prevHeadWorldRot;
    private Quaternion _prevWristWorldRot_left;
    private float _totalThumbTipTranslation, _totalIndexTipTranslation, _totalMiddleTipTranslation;
    private float _totalThumbTipTranslation_left, _totalIndexTipTranslation_left, _totalMiddleTipTranslation_left;
    private float _totalWristWorldTranslation, _totalWristWorldRotation;
    private float _totalWristWorldTranslation_left, _totalWristWorldRotation_left;
    private float _totalDieWorldTranslation, _totalDieWorldRotation;
    private Vector3 _prevDieLocalPos;
    private Quaternion _prevDieLocalRot;
    private float _totalDieLocalTranslation, _totalDieLocalRotation;
    private Vector3 _prevDieLocalPos_left;
    private Quaternion _prevDieLocalRot_left;
    private float _totalDieLocalTranslation_left, _totalDieLocalRotation_left;
    private float _totalHeadWorldTranslation, _totalHeadWorldRotation;
    private float _totalGrabbedTime;
    private float _totalGrabbedTime_left;
    private bool _isGrabTimingActive;
    private bool _isGrabTimingActive_left;
    private float _grabStartTrialTime;
    private float _grabStartTrialTime_left;


    
    public void Initialize(EvaluationSceneManager_HPTK_2 sm)
    {
        _sm = sm;

        string sessionTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _conditionsSuffix = $"_{EXPERIMENT_LABEL}_P{_sm.ParticipantNum}_{_sm.MethodLabel}";
        _basePath = BASE_DIRECTORY_PATH + sessionTimestamp + _conditionsSuffix;
       
        //ResolveRightPuppetReferences();

        RegisterStreamColumns();
        RegisterEventColumns();
        RegisterSummaryColumns();
        
        _eventLog.Open(_basePath + "_EventLog.csv");
        _summaryLog.Open(_basePath + "_Summary.csv");
    }

    public void WriteStreamRow()
    {
        _timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (_streamLog.IsOpen)
        {
            
            AccumulateData();
            
            _streamLog.WriteRow();
        }
    }

    public void CloseAll()
    {
        _streamLog.Close();
        _eventLog.Close();
        _summaryLog.Close();
    }
    
    private void RegisterStreamColumns()
    {
        _streamLog.Col("Timestamp", () => _timestamp);
        _streamLog.Col("Trial Duration", () => _sm.TrialDuration);

        // Wrist (world)
        _streamLog.ColVector3("Wrist World Position", () => _sm.WristWorldPosition);
        _streamLog.ColQuaternion("Wrist World Rotation", () => _sm.WristWorldRotation);
        _streamLog.ColVector3("Wrist World Position_left", () => _sm.WristWorldPosition_left);
        _streamLog.ColQuaternion("Wrist World Rotation_left", () => _sm.WristWorldRotation_left);

        // Fingertips (world)
        _streamLog.ColVector3("Thumb Tip World Position", () => _sm.ThumbTipWorldPosition);
        _streamLog.ColVector3("Index Tip World Position", () => _sm.IndexTipWorldPosition);
        _streamLog.ColVector3("Middle Tip World Position", () => _sm.MiddleTipWorldPosition);
        _streamLog.ColVector3("Thumb Tip World Position_left", () => _sm.ThumbTipWorldPosition_left);
        _streamLog.ColVector3("Index Tip World Position_left", () => _sm.IndexTipWorldPosition_left);
        _streamLog.ColVector3("Middle Tip World Position_left", () => _sm.MiddleTipWorldPosition_left);

        // Fingertips (local / wrist-relative)
        _streamLog.ColVector3("Thumb Tip Local Position", () => _sm.ThumbTipLocalPosition);
        _streamLog.ColVector3("Index Tip Local Position", () => _sm.IndexTipLocalPosition);
        _streamLog.ColVector3("Middle Tip Local Position", () => _sm.MiddleTipLocalPosition);
        _streamLog.ColVector3("Thumb Tip Local Position_left", () => _sm.ThumbTipLocalPosition_left);
        _streamLog.ColVector3("Index Tip Local Position_left", () => _sm.IndexTipLocalPosition_left);
        _streamLog.ColVector3("Middle Tip Local Position_left", () => _sm.MiddleTipLocalPosition_left);

        // Fingertips (euro-filtered)
        _streamLog.ColVector3("Thumb Tip Euro Position", () => _sm.ThumbTipEuroPosition);
        _streamLog.ColVector3("Index Tip Euro Position", () => _sm.IndexTipEuroPosition);
        _streamLog.ColVector3("Middle Tip Euro Position", () => _sm.MiddleTipEuroPosition);
        _streamLog.ColVector3("Thumb Tip Euro Position_left", () => _sm.ThumbTipEuroPosition_left);
        _streamLog.ColVector3("Index Tip Euro Position_left", () => _sm.IndexTipEuroPosition_left);
        _streamLog.ColVector3("Middle Tip Euro Position_left", () => _sm.MiddleTipEuroPosition_left);

        // Triangle
        _streamLog.ColVector3("Triangle Local Position", () => _sm.TriangleCentroidPosition);
        _streamLog.ColQuaternion("Triangle Local Rotation", () => _sm.TriangleRotation);
        _streamLog.ColVector3("Triangle Local Position_left", () => _sm.TriangleCentroidPosition_left);
        _streamLog.ColQuaternion("Triangle Local Rotation_left", () => _sm.TriangleRotation_left);

        // Die (world)
        _streamLog.ColPose("Die World", () => GetDieWorldPose());

        // Die (local / wrist-relative)
        _streamLog.ColPose("Die Local", () => _sm.ObjectLocalPose);
        _streamLog.ColPose("Die Local_left", () => _sm.ObjectLocalPose_left);

        // Target offset
        _streamLog.ColPose("Target Offset", () => _sm.TargetOffset);

        // Head
        _streamLog.ColVector3("Head World Position", () => _sm.HeadPosition);
        _streamLog.ColQuaternion("Head World Rotation", () => _sm.HeadRotation);

        // Gain
       //  _streamLog.Col("Angle Scale Factor", () => _sm.AngleScaleFactor);

        // Status
        _streamLog.Col("Is Grabbed", () => _sm.IsGrabbed);
        _streamLog.Col("Is Grabbed_right", () => _sm.IsGrabbed_right);
        _streamLog.Col("Is Grabbed_left", () => _sm.IsGrabbed_left);
        //_streamLog.Col("Is Rotating", () => GetInteractingHand().IsRotating);
       // _streamLog.Col("Is Clutching", () => GetInteractingHand().IsClutching);
        _streamLog.Col("Is On Target", () => _sm.IsOnTarget);
        //_streamLog.Col("Interacting Hand", () => GetInteractingHandIndex());
    }
    
    
    private void RegisterEventColumns()
    {
        _eventLog.Col("Event Name", () => _eventName);
        _eventLog.Col("Timestamp", () => _timestamp);
        _eventLog.Col("Trial Duration", () => _sm.TrialDuration);

        //if (_sm.Experiment == ExperimentSceneManager.ExpType.Optimization_Exp1)
        //{
          //  _eventLog.Col("Set Num", () => _sm.SetNum);
           // _eventLog.Col("Angle Index", () => _sm.AngleIndex);
           // _eventLog.Col("Axis Index", () => _sm.AxisIndex);
        //}
        //else
        //{
        _eventLog.Col("Block Num", () => _sm.BlockNum);
        _eventLog.Col("Trial Num", () => _sm.TrialNum);
        //}

        //삼각형 필요 없으니까 패스
        //_eventLog.Col("Current Angle", () => _sm.CurrentAngle);
        //_eventLog.ColVector3("Current Axis", () => _sm.CurrentAxis);

        //_eventLog.Col("Event Hand", () => _eventHandIndex);
        _eventLog.ColPose("Die World", () => GetDieWorldPose());
        _eventLog.ColPose("Die Local", () => _sm.ObjectLocalPose);
        _eventLog.ColPose("Die Local_left", () => _sm.ObjectLocalPose_left);
        _eventLog.ColPose("Target Offset", () => _sm.TargetOffset);
    }
    
    
    private void RegisterSummaryColumns()
    {
        // 실험 1 세팅 같으니까 패스
       // if (_sm.Experiment == ExperimentSceneManager.ExpType.Optimization_Exp1)
        //{
          //  _summaryLog.Col("Set Num", () => _sm.SetNum);
            //_summaryLog.Col("Angle Index", () => _sm.AngleIndex);
            //_summaryLog.Col("Axis Index", () => _sm.AxisIndex);
        //}
        //else
        //{
        _summaryLog.Col("Block Num", () => _sm.BlockNum);
        _summaryLog.Col("Trial Num", () => _sm.TrialNum);
        //}
        //_summaryLog.Col("Current Angle", () => _sm.CurrentAngle);
        //_summaryLog.ColVector3("Current Axis", () => _sm.CurrentAxis);
        _summaryLog.Col("Task Completion Time", () => _taskCompletionTime);
        _summaryLog.Col("Is Timeout", () => _sm.IsTimeout);

        _summaryLog.ColPose("Target Offset", () => _sm.TargetOffset);

        // Accumulation
        _summaryLog.Col("Total Thumb Tip Translation", () => _totalThumbTipTranslation);
        _summaryLog.Col("Total Index Tip Translation", () => _totalIndexTipTranslation);
        _summaryLog.Col("Total Middle Tip Translation", () => _totalMiddleTipTranslation);
        _summaryLog.Col("Total Thumb Tip Translation_left", () => _totalThumbTipTranslation_left);
        _summaryLog.Col("Total Index Tip Translation_left", () => _totalIndexTipTranslation_left);
        _summaryLog.Col("Total Middle Tip Translation_left", () => _totalMiddleTipTranslation_left);
        _summaryLog.Col("Total Wrist Translation", () => _totalWristWorldTranslation);
        _summaryLog.Col("Total Wrist Rotation", () => _totalWristWorldRotation);
        _summaryLog.Col("Total Wrist Translation_left", () => _totalWristWorldTranslation_left);
        _summaryLog.Col("Total Wrist Rotation_left", () => _totalWristWorldRotation_left);
        _summaryLog.Col("Total Die Translation", () => _totalDieWorldTranslation);
        _summaryLog.Col("Total Die Rotation", () => _totalDieWorldRotation);
        _summaryLog.Col("Total Die Local Translation", () => _totalDieLocalTranslation);
        _summaryLog.Col("Total Die Local Rotation", () => _totalDieLocalRotation);
        _summaryLog.Col("Total Die Local Translation_left", () => _totalDieLocalTranslation_left);
        _summaryLog.Col("Total Die Local Rotation_left", () => _totalDieLocalRotation_left);
        _summaryLog.Col("Total Head Translation", () => _totalHeadWorldTranslation);
        _summaryLog.Col("Total Head Rotation", () => _totalHeadWorldRotation);
        _summaryLog.Col("Total Grabbed Time", () => _totalGrabbedTime);
        _summaryLog.Col("Total Grabbed Time_left", () => _totalGrabbedTime_left);
    }
    


    public void OnEvent(string eventName, int handIndex = -1)
    {
        _timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _eventName = eventName;
        //_eventHandIndex = handIndex == -1 ? GetInteractingHandIndex() : handIndex;
        _eventLog.WriteRow();
        if (eventName == "Trial Load")
        {
            _streamLog.Close();
            string suffix = $"_Block{_sm.BlockNum}_Trial{_sm.TrialNum}";
            string streamPath = BASE_DIRECTORY_PATH + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + _conditionsSuffix + suffix;
            _streamLog.Open(streamPath + "_StreamData.csv");
        }
        else if (eventName == "Trial Start")
        {
            ResetAccumulationData();
        }
        else if (eventName == EVENT_GRAB)
        {
            StartGrabTimingSegment();
            ResetPrevPositions();
        }
        else if (eventName == EVENT_RELEASE)
        {
            EndGrabTimingSegment();
        }
        else if (eventName == EVENT_GRAB_LEFT)
        {
            StartGrabTimingSegment_left();
            ResetPrevPositions_left();
        }
        else if (eventName == EVENT_RELEASE_LEFT)
        {
            EndGrabTimingSegment_left();
        }
        else if (eventName == "Trial End")
        {
            EndGrabTimingSegment();
            EndGrabTimingSegment_left();
            _taskCompletionTime = _sm.TrialDuration;
            _summaryLog.WriteRow();
            _streamLog.Close();
        }
    }
    
    
    private void AccumulateData()
    {
        if (!_sm.IsGrabbed) return;

        if (_sm.IsGrabbed_right)
        {
            // Fingertips (local)
            Vector3 thumbPos = _sm.ThumbTipLocalPosition;
            _totalThumbTipTranslation += (thumbPos - _prevThumbTipLocal).magnitude;
            _prevThumbTipLocal = thumbPos;

            Vector3 indexPos = _sm.IndexTipLocalPosition;
            _totalIndexTipTranslation += (indexPos - _prevIndexTipLocal).magnitude;
            _prevIndexTipLocal = indexPos;

            Vector3 middlePos = _sm.MiddleTipLocalPosition;
            _totalMiddleTipTranslation += (middlePos - _prevMiddleTipLocal).magnitude;
            _prevMiddleTipLocal = middlePos;

            // Wrist (world)
            Vector3 wristPos = _sm.WristWorldPosition;
            _totalWristWorldTranslation += (wristPos - _prevWristWorldPos).magnitude;
            _prevWristWorldPos = wristPos;

            Quaternion wristRot = _sm.WristWorldRotation;
            _totalWristWorldRotation += AngleFromDelta(wristRot * Quaternion.Inverse(_prevWristWorldRot));
            _prevWristWorldRot = wristRot;
        }

        if (_sm.IsGrabbed_left)
        {
            // Fingertips (local)_left
            Vector3 thumbPos_left = _sm.ThumbTipLocalPosition_left;
            _totalThumbTipTranslation_left += (thumbPos_left - _prevThumbTipLocal_left).magnitude;
            _prevThumbTipLocal_left = thumbPos_left;

            Vector3 indexPos_left = _sm.IndexTipLocalPosition_left;
            _totalIndexTipTranslation_left += (indexPos_left - _prevIndexTipLocal_left).magnitude;
            _prevIndexTipLocal_left = indexPos_left;

            Vector3 middlePos_left = _sm.MiddleTipLocalPosition_left;
            _totalMiddleTipTranslation_left += (middlePos_left - _prevMiddleTipLocal_left).magnitude;
            _prevMiddleTipLocal_left = middlePos_left;

            // Wrist (world)_left
            Vector3 wristPos_left = _sm.WristWorldPosition_left;
            _totalWristWorldTranslation_left += (wristPos_left - _prevWristWorldPos_left).magnitude;
            _prevWristWorldPos_left = wristPos_left;

            Quaternion wristRot_left = _sm.WristWorldRotation_left;
            _totalWristWorldRotation_left += AngleFromDelta(wristRot_left * Quaternion.Inverse(_prevWristWorldRot_left));
            _prevWristWorldRot_left = wristRot_left;
        }

        // Die (world)
        Transform dieT = _sm.DieTransform;
        if (dieT != null)
        {
            Vector3 diePos = dieT.position;
            _totalDieWorldTranslation += (diePos - _prevDieWorldPos).magnitude;
            _prevDieWorldPos = diePos;

            Quaternion dieRot = dieT.rotation;
            _totalDieWorldRotation += AngleFromDelta(dieRot * Quaternion.Inverse(_prevDieWorldRot));
            _prevDieWorldRot = dieRot;

            if (_sm.IsGrabbed_right)
            {
                // Die (local / wrist-relative)
                Pose dieLocal = _sm.ObjectLocalPose;
                _totalDieLocalTranslation += (dieLocal.position - _prevDieLocalPos).magnitude;
                _prevDieLocalPos = dieLocal.position;

                _totalDieLocalRotation += AngleFromDelta(dieLocal.rotation * Quaternion.Inverse(_prevDieLocalRot));
                _prevDieLocalRot = dieLocal.rotation;
            }

            if (_sm.IsGrabbed_left)
            {
                // Die (local / wrist-relative)_left
                Pose dieLocal_left = _sm.ObjectLocalPose_left;
                _totalDieLocalTranslation_left += (dieLocal_left.position - _prevDieLocalPos_left).magnitude;
                _prevDieLocalPos_left = dieLocal_left.position;

                _totalDieLocalRotation_left += AngleFromDelta(dieLocal_left.rotation * Quaternion.Inverse(_prevDieLocalRot_left));
                _prevDieLocalRot_left = dieLocal_left.rotation;
            }
        }

        // Head (world)
        Vector3 headPos = _sm.HeadPosition;
        _totalHeadWorldTranslation += (headPos - _prevHeadWorldPos).magnitude;
        _prevHeadWorldPos = headPos;

        Quaternion headRot = _sm.HeadRotation;
        _totalHeadWorldRotation += AngleFromDelta(headRot * Quaternion.Inverse(_prevHeadWorldRot));
        _prevHeadWorldRot = headRot;
    }
    
    
    private void ResetAccumulationData()
    {
        

        _prevThumbTipLocal = _sm.ThumbTipLocalPosition;
        _prevIndexTipLocal = _sm.IndexTipLocalPosition;
        _prevMiddleTipLocal = _sm.MiddleTipLocalPosition;
        _prevThumbTipLocal_left = _sm.ThumbTipLocalPosition_left;
        _prevIndexTipLocal_left = _sm.IndexTipLocalPosition_left;
        _prevMiddleTipLocal_left = _sm.MiddleTipLocalPosition_left;
        _totalThumbTipTranslation = 0f;
        _totalIndexTipTranslation = 0f;
        _totalMiddleTipTranslation = 0f;
        _totalThumbTipTranslation_left = 0f;
        _totalIndexTipTranslation_left = 0f;
        _totalMiddleTipTranslation_left = 0f;

        _prevWristWorldPos = _sm.WristWorldPosition;
        _prevWristWorldRot = _sm.WristWorldRotation;
        _prevWristWorldPos_left = _sm.WristWorldPosition_left;
        _prevWristWorldRot_left = _sm.WristWorldRotation_left;
        _totalWristWorldTranslation = 0f;
        _totalWristWorldRotation = 0f;
        _totalWristWorldTranslation_left = 0f;
        _totalWristWorldRotation_left = 0f;

        Transform dieT = _sm.DieTransform;
        _prevDieWorldPos = dieT != null ? dieT.position : Vector3.zero;
        _prevDieWorldRot = dieT != null ? dieT.rotation : Quaternion.identity;
        _totalDieWorldTranslation = 0f;
        _totalDieWorldRotation = 0f;

        Pose dieLocal = _sm.ObjectLocalPose;
        _prevDieLocalPos = dieLocal.position;
        _prevDieLocalRot = dieLocal.rotation;
        _totalDieLocalTranslation = 0f;
        _totalDieLocalRotation = 0f;

        Pose dieLocal_left = _sm.ObjectLocalPose_left;
        _prevDieLocalPos_left = dieLocal_left.position;
        _prevDieLocalRot_left = dieLocal_left.rotation;
        _totalDieLocalTranslation_left = 0f;
        _totalDieLocalRotation_left = 0f;

        _prevHeadWorldPos = _sm.HeadPosition;
        _prevHeadWorldRot = _sm.HeadRotation;
        _totalHeadWorldTranslation = 0f;
        _totalHeadWorldRotation = 0f;

        _totalGrabbedTime = 0f;
        _totalGrabbedTime_left = 0f;
        _isGrabTimingActive = false;
        _isGrabTimingActive_left = false;
        _grabStartTrialTime = 0f;
        _grabStartTrialTime_left = 0f;
    }
    


    
    private void ResetPrevPositions()
    {

        _prevThumbTipLocal = _sm.ThumbTipLocalPosition;
        _prevIndexTipLocal = _sm.IndexTipLocalPosition;
        _prevMiddleTipLocal = _sm.MiddleTipLocalPosition;

        _prevWristWorldPos = _sm.WristWorldPosition;
        _prevWristWorldRot = _sm.WristWorldRotation;

        Transform dieT = _sm.DieTransform;
        _prevDieWorldPos = dieT != null ? dieT.position : Vector3.zero;
        _prevDieWorldRot = dieT != null ? dieT.rotation : Quaternion.identity;

        Pose dieLocal = _sm.ObjectLocalPose;
        _prevDieLocalPos = dieLocal.position;
        _prevDieLocalRot = dieLocal.rotation;

        _prevHeadWorldPos = _sm.HeadPosition;
        _prevHeadWorldRot = _sm.HeadRotation;
    }

    private void ResetPrevPositions_left()
    {
        _prevThumbTipLocal_left = _sm.ThumbTipLocalPosition_left;
        _prevIndexTipLocal_left = _sm.IndexTipLocalPosition_left;
        _prevMiddleTipLocal_left = _sm.MiddleTipLocalPosition_left;

        _prevWristWorldPos_left = _sm.WristWorldPosition_left;
        _prevWristWorldRot_left = _sm.WristWorldRotation_left;

        Pose dieLocal_left = _sm.ObjectLocalPose_left;
        _prevDieLocalPos_left = dieLocal_left.position;
        _prevDieLocalRot_left = dieLocal_left.rotation;
    }
    
    private float AngleFromDelta(Quaternion q)
    {
        q.ToAngleAxis(out float angle, out _);
        return angle > 180f ? 360f - angle : angle;
    }

    private void StartGrabTimingSegment()
    {
        if (_isGrabTimingActive) return;
        _grabStartTrialTime = _sm.TrialDuration;
        _isGrabTimingActive = true;
    }

    private void EndGrabTimingSegment()
    {
        if (!_isGrabTimingActive) return;
        _totalGrabbedTime += Mathf.Max(0f, _sm.TrialDuration - _grabStartTrialTime);
        _isGrabTimingActive = false;
    }

    private void StartGrabTimingSegment_left()
    {
        if (_isGrabTimingActive_left) return;
        _grabStartTrialTime_left = _sm.TrialDuration;
        _isGrabTimingActive_left = true;
    }

    private void EndGrabTimingSegment_left()
    {
        if (!_isGrabTimingActive_left) return;
        _totalGrabbedTime_left += Mathf.Max(0f, _sm.TrialDuration - _grabStartTrialTime_left);
        _isGrabTimingActive_left = false;
    }
    /*
    private int GetInteractingHandIndex()
    {
        var hands = _sm.HandInteractors;
        for (int i = 0; i < hands.Count; i++)
        {
            if (hands[i].IsGrabbed) return i;
        }
        return -1;
    }

    private HandInteractor GetInteractingHand()
    {
        var hands = _sm.HandInteractors;
        foreach (var h in hands)
        {
            if (h.IsGrabbed) return h;
        }
        return hands[0];
    }
    
    */
    private Pose GetDieWorldPose()
    {
        Transform t = _sm.DieTransform;
        if (t == null) return new Pose(Vector3.zero, Quaternion.identity);
        return new Pose(t.position, t.rotation);
    }

    
    
    

    private static Vector3 GetWorldPosition(Transform t)
    {
        return t != null ? t.position : Vector3.zero;
    }

    private static Quaternion GetWorldRotation(Transform t)
    {
        return t != null ? t.rotation : Quaternion.identity;
    }

}
