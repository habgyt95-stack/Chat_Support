import React, {useState, useRef, useEffect} from 'react';
import {BsMic, BsStopFill, BsSend, BsX, BsPlayFill, BsPauseFill} from 'react-icons/bs';
import {chatApi, MessageType} from '../../services/chatApi';
import {Button} from 'react-bootstrap';
const VoiceRecorderComponent = ({onVoiceRecorded, disabled, chatRoomId, onActiveChange}) => {
  const [isRecording, setIsRecording] = useState(false);
  const [isPaused, setIsPaused] = useState(false);
  const [recordingTime, setRecordingTime] = useState(0);
  const [audioBlob, setAudioBlob] = useState(null);
  const [audioUrl, setAudioUrl] = useState(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackTime, setPlaybackTime] = useState(0);
  const [duration, setDuration] = useState(0);

  const [isUploading, setIsUploading] = useState(false);

  const mediaRecorderRef = useRef(null);
  const audioChunksRef = useRef([]);
  const timerRef = useRef(null);
  const audioRef = useRef(null);
  const streamRef = useRef(null);
  const animationRef = useRef(null);

  useEffect(() => {
    return () => {
      // Cleanup
      if (streamRef.current) {
        streamRef.current.getTracks().forEach((track) => track.stop());
      }
      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
      if (animationRef.current) {
        cancelAnimationFrame(animationRef.current);
      }
    };
  }, []);

  const startRecording = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          sampleRate: 44100,
        },
      });

      streamRef.current = stream;

      const mediaRecorder = new MediaRecorder(stream, {
        mimeType: 'audio/webm;codecs=opus',
      });

      mediaRecorderRef.current = mediaRecorder;
      audioChunksRef.current = [];

      mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      mediaRecorder.onstop = () => {
        const audioBlob = new Blob(audioChunksRef.current, {type: 'audio/webm'});
        setAudioBlob(audioBlob);

        const url = URL.createObjectURL(audioBlob);
        setAudioUrl(url);

        // Stop all tracks
        stream.getTracks().forEach((track) => track.stop());
      };

  mediaRecorder.start(100); // Collect data every 100ms
      setIsRecording(true);
      setRecordingTime(0);
  if (onActiveChange) onActiveChange(true);

      // Start timer
      timerRef.current = setInterval(() => {
        setRecordingTime((prev) => prev + 1);
      }, 1000);
    } catch (error) {
      console.error('Error accessing microphone:', error);
      alert('Unable to access microphone. Please check permissions.');
    }
  };

  const stopRecording = () => {
    if (mediaRecorderRef.current && isRecording) {
      mediaRecorderRef.current.stop();
      setIsRecording(false);

      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
    }
  };

  const pauseResumeRecording = () => {
    if (!mediaRecorderRef.current) return;

    if (isPaused) {
      mediaRecorderRef.current.resume();
      setIsPaused(false);

      timerRef.current = setInterval(() => {
        setRecordingTime((prev) => prev + 1);
      }, 1000);
    } else {
      mediaRecorderRef.current.pause();
      setIsPaused(true);

      if (timerRef.current) {
        clearInterval(timerRef.current);
      }
    }
  };

  const sendVoiceMessage = async () => {
    if (!audioBlob || !chatRoomId) return;

    try {
      setIsUploading(true);

  const result = await chatApi.uploadFile(audioBlob, chatRoomId, MessageType.Audio, () => {});

      // Send to parent component
      onVoiceRecorded({
        url: result.fileUrl,
        duration: recordingTime,
        size: audioBlob.size,
        type: MessageType.AUDIO,
        mimeType: 'audio/webm',
      });

      // Reset
      resetRecording();
      setIsUploading(false);
    } catch (error) {
      console.error('Failed to send voice message:', error);
      alert('Failed to send voice message');
      setIsUploading(false);
    }
  };

  const resetRecording = () => {
    setIsRecording(false);
    setIsPaused(false);
    setRecordingTime(0);
    setAudioBlob(null);
    setAudioUrl(null);
    setIsPlaying(false);
    setPlaybackTime(0);
  if (onActiveChange) onActiveChange(false);

    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current.currentTime = 0;
    }

    if (timerRef.current) {
      clearInterval(timerRef.current);
    }

    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop());
    }
  };

  const togglePlayback = () => {
    if (!audioRef.current) return;

    if (isPlaying) {
      audioRef.current.pause();
      setIsPlaying(false);
    } else {
      audioRef.current.play();
      setIsPlaying(true);
    }
  };

  const formatTime = (seconds) => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  const updatePlaybackTime = () => {
    if (audioRef.current) {
      setPlaybackTime(Math.floor(audioRef.current.currentTime));

      if (!audioRef.current.paused) {
        animationRef.current = requestAnimationFrame(updatePlaybackTime);
      }
    }
  };

  // Recording or preview inline bar UI (single row)
  if (isRecording || audioBlob) {
    return (
      <div className="d-flex align-items-center gap-2 bg-light rounded-pill px-2 py-1 w-100" style={{minHeight: 44}}>
        {/* Cancel / Close */}
        <Button variant="link" onClick={resetRecording} className="p-1 border-0" title="لغو">
          <BsX className="w-5 h-5" />
        </Button>

        {/* Record or Play/Pause */}
        {isRecording ? (
          <>
            <Button variant="link" onClick={pauseResumeRecording} className="p-1 border-0" title={isPaused ? 'ادامه' : 'توقف موقت'}>
              {isPaused ? <BsPlayFill className="w-5 h-5" /> : <BsPauseFill className="w-5 h-5" />}
            </Button>
            <Button variant="link" onClick={stopRecording} className="p-1 border-0" title="پایان ضبط">
              <BsStopFill className="w-5 h-5" />
            </Button>
            <div className="d-flex align-items-center gap-2 ms-2">
              <span className="badge bg-danger">REC</span>
              <span className="text-muted small">{formatTime(recordingTime)}</span>
            </div>
          </>
        ) : (
          <>
            <Button variant="link" onClick={togglePlayback} className="p-1 border-0" title={isPlaying ? 'توقف' : 'پخش'} disabled={isUploading}>
              {isPlaying ? <BsPauseFill className="w-5 h-5" /> : <BsPlayFill className="w-5 h-5" />}
            </Button>
            <audio
              ref={audioRef}
              src={audioUrl}
              onLoadedMetadata={(e) => setDuration(Math.floor(e.target.duration))}
              onPlay={updatePlaybackTime}
              onPause={() => cancelAnimationFrame(animationRef.current)}
              onEnded={() => {
                setIsPlaying(false);
                setPlaybackTime(0);
              }}
              className="d-none"
            />
            <div className="flex-1 d-flex align-items-center" style={{minWidth: 120}}>
              <div className="bg-secondary bg-opacity-25 rounded-pill w-100" style={{height: 6}}>
                <div
                  className="bg-primary rounded-pill"
                  style={{height: 6, width: `${duration > 0 ? (playbackTime / duration) * 100 : 0}%`, transition: 'width .15s linear'}}
                />
              </div>
            </div>
            <span className="text-muted small ms-2">{formatTime(playbackTime)}/{formatTime(duration || recordingTime)}</span>
            {isUploading ? <span className="small ms-2">در حال ارسال...</span> : (
              <Button variant="link" onClick={sendVoiceMessage} className="p-1 border-0 ms-2" title="ارسال" disabled={!chatRoomId}>
                <BsSend className="w-5 h-5" />
              </Button>
            )}
          </>
        )}
      </div>
    );
  }

  // Default trigger button
  return (
    <Button variant="link" onClick={startRecording} disabled={disabled} className="p-2 border-0" title="پیام صوتی">
      <BsMic size={20} className="w-5 h-5" />
    </Button>
  );
};

// کامپوننت پخش صدا در پیام
export const VoiceMessagePlayer = ({audioUrl, duration}) => {
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [audioDuration, setAudioDuration] = useState(duration || 0);
  const audioRef = useRef(null);
  const progressRef = useRef(null);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;

    const updateProgress = () => {
      setCurrentTime(audio.currentTime);

      if (progressRef.current) {
        const percentage = (audio.currentTime / audio.duration) * 100;
        progressRef.current.style.width = `${percentage}%`;
      }
    };

    const handleEnded = () => {
      setIsPlaying(false);
      setCurrentTime(0);
      if (audioRef.current) {
        audioRef.current.currentTime = 0;
      }
      if (progressRef.current) {
        progressRef.current.style.width = '0%';
      }
    };

    audio.addEventListener('timeupdate', updateProgress);
    audio.addEventListener('ended', handleEnded);
    audio.addEventListener('loadedmetadata', () => {
      setAudioDuration(Math.floor(audio.duration));
    });

    return () => {
      audio.removeEventListener('timeupdate', updateProgress);
      audio.removeEventListener('ended', handleEnded);
    };
  }, []);

  const togglePlayback = () => {
    if (!audioRef.current) return;

    if (isPlaying) {
      audioRef.current.pause();
    } else {
      audioRef.current.play();
    }
    setIsPlaying(!isPlaying);
  };

  const handleProgressClick = (e) => {
    if (!audioRef.current) return;

    const rect = e.currentTarget.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const percentage = x / rect.width;
    const newTime = percentage * audioRef.current.duration;

    audioRef.current.currentTime = newTime;
    setCurrentTime(newTime);
  };

  const formatTime = (seconds) => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  return (
    <div className="flex items-center gap-3 bg-gray-100 rounded-lg p-3 min-w-[280px]">
      <Button variant="link" onClick={togglePlayback} className="p-2 bg-white rounded-full hover:bg-gray-200 transition flex-shrink-0 border-0">
        {isPlaying ? <BsPauseFill style={{color: '#1976d2'}} className="w-5 h-5" /> : <BsPlayFill style={{color: '#1976d2'}} className="w-5 h-5" />}
      </Button>

      <div className="flex-1">
        <div className="bg-gray-300 rounded-full h-1.5 cursor-pointer relative" onClick={handleProgressClick}>
          <div ref={progressRef} className="bg-blue-500 h-1.5 rounded-full transition-all absolute top-0 left-0" style={{width: '0%'}} />
        </div>

        <div className="flex justify-between text-xs text-gray-600 mt-1">
          <span>{formatTime(currentTime)}</span>
          <span>{formatTime(audioDuration)}</span>
        </div>
      </div>

      <audio ref={audioRef} src={audioUrl} preload="metadata" />
    </div>
  );
};

export default VoiceRecorderComponent;
