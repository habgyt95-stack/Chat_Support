import React, {useRef, useState} from 'react';
import {Button, OverlayTrigger, Tooltip, Spinner} from 'react-bootstrap';
import {Paperclip} from 'lucide-react';
import {chatApi, fileHelpers} from '../../services/chatApi';
import './Chat.css';

// Simplified: open native picker directly, auto-upload, no modal
const FileUploadComponent = ({onFileUploaded, disabled = false, chatRoomId, maxFileSizeMB = 20}) => {
  const fileInputRef = useRef(null);
  const [isUploading, setIsUploading] = useState(false);

  const onPickFile = () => {
    if (disabled || isUploading) return;
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      fileHelpers.validateFile(file, maxFileSizeMB);
    } catch (err) {
      alert(err.message || 'فایل نامعتبر است');
      // reset input so same file can be picked again later
      e.target.value = '';
      return;
    }

    setIsUploading(true);

    try {
      const messageType = fileHelpers.getMessageTypeFromFile(file);
      const result = await chatApi.uploadFile(file, chatRoomId, messageType, () => {});

      await onFileUploaded({
        url: result.fileUrl,
        name: file.name,
        size: file.size,
        type: messageType,
        mimeType: file.type,
      });
    } catch (error) {
      console.error('Upload error:', error);
      alert(error.response?.data || error.message || 'خطا در آپلود فایل');
    } finally {
      setIsUploading(false);
      // reset input so same file can be picked again
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  return (
    <>
      <input
        ref={fileInputRef}
        type="file"
        onChange={handleFileChange}
        className="d-none"
        accept="image/*,video/*,audio/*,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.zip,.rar"
        disabled={disabled || isUploading}
      />

      <OverlayTrigger placement="top" overlay={<Tooltip>پیوست فایل</Tooltip>}>
        <Button variant="link" onClick={onPickFile} disabled={disabled || isUploading} className="p-2">
          {isUploading ? <Spinner size="sm" /> : <Paperclip size={20} />}
        </Button>
      </OverlayTrigger>
    </>
  );
};

export default FileUploadComponent;
