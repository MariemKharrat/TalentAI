import { DragEvent, useRef, useState } from 'react';

interface FileUploadProps {
  onFileSelect: (file: File) => Promise<void> | void;
  loading?: boolean;
}

function FileUpload({ onFileSelect, loading = false }: FileUploadProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [isDragging, setIsDragging] = useState(false);

  const handleFile = async (file?: File | null) => {
    if (!file || loading) {
      return;
    }

    await onFileSelect(file);
  };

  const handleDrop = async (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(false);
    await handleFile(event.dataTransfer.files?.[0]);
  };

  return (
    <div
      className={`file-upload${isDragging ? ' file-upload-active' : ''}${loading ? ' file-upload-disabled' : ''}`}
      onClick={() => inputRef.current?.click()}
      onDragOver={(event) => {
        event.preventDefault();
        if (!loading) {
          setIsDragging(true);
        }
      }}
      onDragLeave={() => setIsDragging(false)}
      onDrop={handleDrop}
      role="button"
      tabIndex={0}
      onKeyDown={(event) => {
        if ((event.key === 'Enter' || event.key === ' ') && !loading) {
          inputRef.current?.click();
        }
      }}
    >
      <input
        ref={inputRef}
        type="file"
        accept=".pdf,.doc,.docx"
        hidden
        onChange={async (event) => {
          await handleFile(event.target.files?.[0]);
          event.target.value = '';
        }}
      />
      <strong>{loading ? 'Uploading CV...' : 'Drag & drop a CV here'}</strong>
      <span>{loading ? 'Parsing candidate profile' : 'or click to browse PDF, DOC, or DOCX files'}</span>
    </div>
  );
}

export default FileUpload;
