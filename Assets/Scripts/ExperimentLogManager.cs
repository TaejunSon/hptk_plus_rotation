using System;
using UnityEngine;

public class ExperimentLogManager : MonoBehaviour
{   
    private EvaluationSceneManager_HPTK_2 _sm;

    private const string BASE_DIRECTORY_PATH = @"C:\Users\master\hptk_expsetting\hptk_plus_rotation\Data\";
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
    private Vector3 _prevWristWorldPos, _prevDieWorldPos, _prevHeadWorldPos;
    private Quaternion _prevWristWorldRot, _prevDieWorldRot, _prevHeadWorldRot;
    private float _totalThumbTipTranslation, _totalIndexTipTranslation, _totalMiddleTipTranslation;
    private float _totalWristWorldTranslation, _totalWristWorldRotation;
    private float _totalDieWorldTranslation, _totalDieWorldRotation;
    private Vector3 _prevDieLocalPos;
    private Quaternion _prevDieLocalRot;
    private float _totalDieLocalTranslation, _totalDieLocalRotation;
    private float _totalHeadWorldTranslation, _totalHeadWorldRotation;
    private float _totalGrabbedTime;
    private bool _isGrabTimingActive;
    private float _grabStartTrialTime;


    
    public void Initialize(EvaluationSceneManager_HPTK_2 sm)
    {
        _sm = sm;

        _basePath = BASE_DIRECTORY_PATH + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"); 
       
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

        // Fingertips (world)
        _streamLog.ColVector3("Thumb Tip World Position", () => _sm.ThumbTipWorldPosition);
        _streamLog.ColVector3("Index Tip World Position", () => _sm.IndexTipWorldPosition);
        _streamLog.ColVector3("Middle Tip World Position", () => _sm.MiddleTipWorldPosition);

        // Fingertips (local / wrist-relative)
        _streamLog.ColVector3("Thumb Tip Local Position", () => _sm.ThumbTipLocalPosition);
        _streamLog.ColVector3("Index Tip Local Position", () => _sm.IndexTipLocalPosition);
        _streamLog.ColVector3("Middle Tip Local Position", () => _sm.MiddleTipLocalPosition);

        // Fingertips (euro-filtered)
        _streamLog.ColVector3("Thumb Tip Euro Position", () => _sm.ThumbTipEuroPosition);
        _streamLog.ColVector3("Index Tip Euro Position", () => _sm.IndexTipEuroPosition);
        _streamLog.ColVector3("Middle Tip Euro Position", () => _sm.MiddleTipEuroPosition);

        // Triangle
        _streamLog.ColVector3("Triangle Local Position", () => _sm.TriangleCentroidPosition);
        _streamLog.ColQuaternion("Triangle Local Rotation", () => _sm.TriangleRotation);

        // Die (world)
        _streamLog.ColPose("Die World", () => GetDieWorldPose());

        // Die (local / wrist-relative)
        _streamLog.ColPose("Die Local", () => _sm.ObjectLocalPose);

        // Target offset
        _streamLog.ColPose("Target Offset", () => _sm.TargetOffset);

        // Head
        _streamLog.ColVector3("Head World Position", () => _sm.HeadPosition);
        _streamLog.ColQuaternion("Head World Rotation", () => _sm.HeadRotation);

        // Gain
       //  _streamLog.Col("Angle Scale Factor", () => _sm.AngleScaleFactor);

        // Status
        _streamLog.Col("Is Grabbed", () => _sm.IsGrabbed);
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
        _eventLog.Col("Trial Num", () => _sm.TrialNum);
        //}

        //삼각형 필요 없으니까 패스
        //_eventLog.Col("Current Angle", () => _sm.CurrentAngle);
        //_eventLog.ColVector3("Current Axis", () => _sm.CurrentAxis);

        //_eventLog.Col("Event Hand", () => _eventHandIndex);
        _eventLog.ColPose("Die World", () => GetDieWorldPose());
        _eventLog.ColPose("Die Local", () => _sm.ObjectLocalPose);
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
        _summaryLog.Col("Total Wrist Translation", () => _totalWristWorldTranslation);
        _summaryLog.Col("Total Wrist Rotation", () => _totalWristWorldRotation);
        _summaryLog.Col("Total Die Translation", () => _totalDieWorldTranslation);
        _summaryLog.Col("Total Die Rotation", () => _totalDieWorldRotation);
        _summaryLog.Col("Total Die Local Translation", () => _totalDieLocalTranslation);
        _summaryLog.Col("Total Die Local Rotation", () => _totalDieLocalRotation);
        _summaryLog.Col("Total Head Translation", () => _totalHeadWorldTranslation);
        _summaryLog.Col("Total Head Rotation", () => _totalHeadWorldRotation);
        _summaryLog.Col("Total Grabbed Time", () => _totalGrabbedTime);
    }
    


    public void OnEvent(string eventName, int handIndex = -1)
    {
        _timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _eventName = eventName;
        //_eventHandIndex = handIndex == -1 ? GetInteractingHandIndex() : handIndex;
        _eventLog.WriteRow();
        _conditionsSuffix = "HPTK";
        if (eventName == "Trial Load")
        {
            _streamLog.Close();
            //string suffix = (_sm.Experiment == ExperimentSceneManager.ExpType.Optimization_Exp1)
              //  ? $"_Set{_sm.SetNum}_Angle{_sm.CurrentAngle}_Axis{_sm.AxisIndex}"
                //: $"_Trial{_sm.TrialNum}";
            string streamPath = BASE_DIRECTORY_PATH + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + _conditionsSuffix; // + suffix;
            _streamLog.Open(streamPath + "_StreamData.csv");
        }
        else if (eventName == "Trial Start")
        {
            ResetAccumulationData();
        }
        else if (eventName == "Grab")
        {
            StartGrabTimingSegment();
            ResetPrevPositions();
        }
        else if (eventName == "Release")
        {
            EndGrabTimingSegment();
        }
        else if (eventName == "Trial End")
        {
            EndGrabTimingSegment();
            _taskCompletionTime = _sm.TrialDuration;
            _summaryLog.WriteRow();
            _streamLog.Close();
        }
    }
    
    
    private void AccumulateData()
    {
        
        if (!_sm.IsGrabbed) return;

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

            // Die (local / wrist-relative)
            Pose dieLocal = _sm.ObjectLocalPose;
            _totalDieLocalTranslation += (dieLocal.position - _prevDieLocalPos).magnitude;
            _prevDieLocalPos = dieLocal.position;

            _totalDieLocalRotation += AngleFromDelta(dieLocal.rotation * Quaternion.Inverse(_prevDieLocalRot));
            _prevDieLocalRot = dieLocal.rotation;
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
        _totalThumbTipTranslation = 0f;
        _totalIndexTipTranslation = 0f;
        _totalMiddleTipTranslation = 0f;

        _prevWristWorldPos = _sm.WristWorldPosition;
        _prevWristWorldRot = _sm.WristWorldRotation;
        _totalWristWorldTranslation = 0f;
        _totalWristWorldRotation = 0f;

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

        _prevHeadWorldPos = _sm.HeadPosition;
        _prevHeadWorldRot = _sm.HeadRotation;
        _totalHeadWorldTranslation = 0f;
        _totalHeadWorldRotation = 0f;

        _totalGrabbedTime = 0f;
        _isGrabTimingActive = false;
        _grabStartTrialTime = 0f;
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
