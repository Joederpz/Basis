using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace uLipSync
{

    public class uLipSync : MonoBehaviour
    {
        public Profile profile;
        public LipSyncUpdateEvent onLipSyncUpdate = new LipSyncUpdateEvent();
        [Range(0f, 1f)] public float outputSoundGain = 1f;
        uLipSyncAudioSource _currentAudioSourceProxy;

        JobHandle _jobHandle;
        object _lockObject = new object();
        bool _allocated = false;
        int _index = 0;
        bool _isDataReceived = false;

        NativeArray<float> _rawInputData;
        NativeArray<float> _inputData;
        NativeArray<float> _mfcc;
        NativeArray<float> _mfccForOther;
        NativeArray<float> _means;
        NativeArray<float> _standardDeviations;
        NativeArray<float> _phonemes;
        NativeArray<float> _scores;
        NativeArray<LipSyncJob.Info> _info;
        List<int> _requestedCalibrationVowels = new List<int>();
        Dictionary<string, float> _ratios = new Dictionary<string, float>();

        public NativeArray<float> mfcc => _mfccForOther;
        public LipSyncInfo result { get; private set; } = new LipSyncInfo();

#if UNITY_WEBGL
    public bool autoAudioSyncOnWebGL = true;
    [Range(-0.1f, 0.3f)] public float audioSyncOffsetTime = 0f;
#if !UNITY_EDITOR
    float[] _audioBuffer = null;
#endif
    bool _isWebGLProcessed = false;
#endif

#if ULIPSYNC_DEBUG
    NativeArray<float> _debugData;
    NativeArray<float> _debugSpectrum;
    NativeArray<float> _debugMelSpectrum;
    NativeArray<float> _debugMelCepstrum;
    NativeArray<float> _debugDataForOther;
    NativeArray<float> _debugSpectrumForOther;
    NativeArray<float> _debugMelSpectrumForOther;
    NativeArray<float> _debugMelCepstrumForOther;
    public NativeArray<float> data => _debugDataForOther;
    public NativeArray<float> spectrum => _debugSpectrumForOther;
    public NativeArray<float> melSpectrum => _debugMelSpectrumForOther;
    public NativeArray<float> melCepstrum => _debugMelCepstrumForOther;
#endif

        int inputSampleCount
        {
            get
            {
                if (!profile) return AudioSettings.outputSampleRate;
                float r = (float)AudioSettings.outputSampleRate / profile.targetSampleRate;
                return Mathf.CeilToInt(profile.sampleCount * r);
            }
        }

        int mfccNum => profile ? profile.mfccNum : 12;

        void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
        InitializeWebGL();
#endif
        }

        void OnEnable()
        {
            AllocateBuffers();
        }

        void OnDisable()
        {
            _jobHandle.Complete();
            DisposeBuffers();
        }

       public void DoUpdate()
        {
            if (!profile) return;
            if (!_jobHandle.IsCompleted) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateWebGL();
#endif
            UpdateResult();
            InvokeCallback();
            UpdateCalibration();
            UpdatePhonemes();
            ScheduleJob();

            UpdateBuffers();
        }
        public float[] UpdateResultsBuffer;
        public int phonemeCount;
        void AllocateBuffers()
        {
            if (_allocated)
            {
                DisposeBuffers();
            }
            _allocated = true;

            _jobHandle.Complete();

            lock (_lockObject)
            {
                int n = inputSampleCount;
                phonemeCount = profile ? profile.mfccs.Count : 1;
                _rawInputData = new NativeArray<float>(n, Allocator.Persistent);
                _inputData = new NativeArray<float>(n, Allocator.Persistent);
                _mfcc = new NativeArray<float>(mfccNum, Allocator.Persistent);
                _mfccForOther = new NativeArray<float>(mfccNum, Allocator.Persistent);
                _means = new NativeArray<float>(mfccNum, Allocator.Persistent);
                _standardDeviations = new NativeArray<float>(mfccNum, Allocator.Persistent);
                _scores = new NativeArray<float>(phonemeCount, Allocator.Persistent);
                UpdateResultsBuffer = new float[phonemeCount];
                _phonemes = new NativeArray<float>(mfccNum * phonemeCount, Allocator.Persistent);
                _info = new NativeArray<LipSyncJob.Info>(1, Allocator.Persistent);
#if ULIPSYNC_DEBUG
            _debugData = new NativeArray<float>(profile.sampleCount, Allocator.Persistent);
            _debugDataForOther = new NativeArray<float>(profile.sampleCount, Allocator.Persistent);
            _debugSpectrum = new NativeArray<float>(profile.sampleCount, Allocator.Persistent);
            _debugSpectrumForOther = new NativeArray<float>(profile.sampleCount, Allocator.Persistent);
            _debugMelSpectrum = new NativeArray<float>(profile.melFilterBankChannels, Allocator.Persistent);
            _debugMelSpectrumForOther = new NativeArray<float>(profile.melFilterBankChannels, Allocator.Persistent);
            _debugMelCepstrum = new NativeArray<float>(profile.melFilterBankChannels, Allocator.Persistent);
            _debugMelCepstrumForOther = new NativeArray<float>(profile.melFilterBankChannels, Allocator.Persistent);
#endif
            }
        }

        void DisposeBuffers()
        {
            if (!_allocated) return;
            _allocated = false;

            _jobHandle.Complete();

            lock (_lockObject)
            {
                _rawInputData.Dispose();
                _inputData.Dispose();
                _mfcc.Dispose();
                _mfccForOther.Dispose();
                _means.Dispose();
                _standardDeviations.Dispose();
                _scores.Dispose();
                _phonemes.Dispose();
                _info.Dispose();
#if ULIPSYNC_DEBUG
            _debugData.Dispose();
            _debugDataForOther.Dispose();
            _debugSpectrum.Dispose();
            _debugSpectrumForOther.Dispose();
            _debugMelSpectrum.Dispose();
            _debugMelSpectrumForOther.Dispose();
            _debugMelCepstrum.Dispose();
            _debugMelCepstrumForOther.Dispose();
#endif
            }
        }

        void UpdateBuffers()
        {
            if (inputSampleCount != _rawInputData.Length ||
                profile.mfccs.Count * mfccNum != _phonemes.Length
#if ULIPSYNC_DEBUG
            || profile.melFilterBankChannels != _debugMelSpectrum.Length
#endif
            )
            {
                lock (_lockObject)
                {
                    DisposeBuffers();
                    AllocateBuffers();
                }
            }
        }
        void UpdateResult()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
    if (!_isWebGLProcessed)
    {
        result = new LipSyncInfo()
        {
            phoneme = result.phoneme,
            volume = 0f,
            rawVolume = 0f,
            phonemeRatios = _ratios,
        };
        return;
    }
#endif

            _jobHandle.Complete(); // Wait for async job completion
            _mfccForOther.CopyFrom(_mfcc);

#if ULIPSYNC_DEBUG
    _debugDataForOther.CopyFrom(_debugData);
    _debugSpectrumForOther.CopyFrom(_debugSpectrum);
    _debugMelSpectrumForOther.CopyFrom(_debugMelSpectrum);
    _debugMelCepstrumForOther.CopyFrom(_debugMelCepstrum);
#endif

            // Main phoneme identification
            int index = _info[0].mainPhonemeIndex;
            string mainPhoneme = profile.GetPhoneme(index);

            // Calculate sumScore and populate UpdateResultsBuffer
            float sumScore = 0f;
            _scores.CopyTo(UpdateResultsBuffer);
            for (int i = 0; i < phonemeCount; ++i)
            {
                sumScore += UpdateResultsBuffer[i];
            }

            // Clear and update _ratios
            _ratios.Clear();
            float invSumScore = sumScore > 0f ? 1f / sumScore : 0f; // Precompute inverse for efficiency
            for (int i = 0; i < phonemeCount; ++i)
            {
                string phoneme = profile.GetPhoneme(i);
                float ratio = UpdateResultsBuffer[i] * invSumScore; // Avoid division in loop
                if (!_ratios.TryGetValue(phoneme, out float existingRatio))
                {
                    _ratios[phoneme] = ratio; // Add new
                }
                else
                {
                    _ratios[phoneme] = existingRatio + ratio; // Accumulate
                }
            }

            // Normalize volume
            float rawVol = _info[0].volume;
            float minVol = Common.DefaultMinVolume;
            float maxVol = Common.DefaultMaxVolume;
            float normVol = math.clamp((math.log10(rawVol) - minVol) / (maxVol - minVol), 0f, 1f);

            // Update result
            result = new LipSyncInfo()
            {
                phoneme = mainPhoneme,
                volume = normVol,
                rawVolume = rawVol,
                phonemeRatios = _ratios,
            };
        }

        void InvokeCallback()
        {
            onLipSyncUpdate?.Invoke(result);
        }

        void UpdatePhonemes()
        {
            int index = 0;
            int phonemeLength = _phonemes.Length;
            int count = profile.mfccs.Count;

            for (int i = 0; i < count && index < phonemeLength; i++)
            {
                NativeArray<float> mfccNativeArray = profile.mfccs[i].mfccNativeArray;

                // Determine how many elements to copy
                int remainingLength = phonemeLength - index;
                int copyLength = math.min(12, remainingLength);

                // Use NativeArray.CopyTo for batch copying
                NativeArray<float>.Copy(mfccNativeArray, 0, _phonemes, index, copyLength);

                index += copyLength;
            }
        }


        void ScheduleJob()
        {
            if (!_isDataReceived) return;
            _isDataReceived = false;

            int index = 0;
            lock (_lockObject)
            {
                _inputData.CopyFrom(_rawInputData);
                _means.CopyFrom(profile.means);
                _standardDeviations.CopyFrom(profile.standardDeviation);
                index = _index;
            }

            var lipSyncJob = new LipSyncJob()
            {
                input = _inputData,
                startIndex = index,
                outputSampleRate = AudioSettings.outputSampleRate,
                targetSampleRate = profile.targetSampleRate,
                melFilterBankChannels = profile.melFilterBankChannels,
                means = _means,
                standardDeviations = _standardDeviations,
                mfcc = _mfcc,
                phonemes = _phonemes,
                compareMethod = profile.compareMethod,
                scores = _scores,
                info = _info,
#if ULIPSYNC_DEBUG
            debugData = _debugData,
            debugSpectrum = _debugSpectrum,
            debugMelSpectrum = _debugMelSpectrum,
            debugMelCepstrum = _debugMelCepstrum,
#endif
            };

            _jobHandle = lipSyncJob.Schedule();
        }

        public void RequestCalibration(int index)
        {
            _requestedCalibrationVowels.Add(index);
        }

        void UpdateCalibration()
        {
            if (!profile) return;

            foreach (var index in _requestedCalibrationVowels)
            {
                profile.UpdateMfcc(index, mfcc, true);
            }

            _requestedCalibrationVowels.Clear();
        }

        public void OnDataReceived(float[] input, int channels)
        {
            if (_rawInputData.Length == 0) return;

            lock (_lockObject)
            {
                int n = _rawInputData.Length;
                _index = _index % n;
                for (int i = 0; i < input.Length; i += channels)
                {
                    _rawInputData[_index++ % n] = input[i];
                }
            }

            if (math.abs(outputSoundGain - 1f) > math.EPSILON)
            {
                int n = input.Length;
                for (int i = 0; i < n; ++i)
                {
                    input[i] *= outputSoundGain;
                }
            }

            _isDataReceived = true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
    public void InitializeWebGL()
    {
        if (!_audioSource) return;

        if (autoAudioSyncOnWebGL)
        {
            WebGL.Register(this);
        }
    }

    public void OnAuidoContextInitiallyResumed()
    {
        if (!_audioSource) return;

        _audioSource.timeSamples = _audioSource.timeSamples;

        Debug.Log("AudioSource.timeSamples has been automatically synchronized.");
    }

    void UpdateWebGL()
    {
        _isWebGLProcessed = false;

        if (!_audioSource || !_audioSource.isPlaying) return;

        var clip = _audioSource.clip;
        if (!clip || clip.loadState != AudioDataLoadState.Loaded) return;

        int ch = clip.channels;
        int fps = Application.targetFrameRate;
        if (fps <= 0) fps = 60;
        int n = AudioSettings.outputSampleRate * ch / fps;

        if (_audioBuffer == null || _audioBuffer.Length != n)
        {
            _audioBuffer = new float[n];
        }

        int offset = _audioSource.timeSamples;
        offset += (int)(audioSyncOffsetTime * AudioSettings.outputSampleRate * ch);
        offset = math.min(offset, clip.samples - n - 2);
        clip.GetData(_audioBuffer, offset);
        OnDataReceived(_audioBuffer, ch);

        _isWebGLProcessed = true;
    }
#endif

#if UNITY_EDITOR
        public void OnBakeStart(Profile profile)
        {
            this.profile = profile;
            AllocateBuffers();
        }

        public void OnBakeEnd()
        {
            DisposeBuffers();
        }

        public void OnBakeUpdate(float[] input, int channels)
        {
            OnDataReceived(input, channels);
            UpdateBuffers();
            UpdatePhonemes();
            ScheduleJob();
            _jobHandle.Complete();
            UpdateResult();
        }
#endif
    }

}