import { Component, ElementRef, ViewChild, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  sources?: string[];
}

interface UploadedDocument {
  id: string;
  filename: string;
  uploadDate: string;
  chunkCount: number;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit {
  title = 'Vector Retrieval System';
  
  isDragging = false;
  uploading = false;
  uploadSuccess = false;
  sidebarOpen = false;
  
  documents: UploadedDocument[] = [];

  chatMessages: ChatMessage[] = [];
  currentQuery = '';
  isTyping = false;

  @ViewChild('chatContainer') private chatContainer!: ElementRef;
  @ViewChild('queryInput') private queryInput!: ElementRef;

  private apiUrl = '/api';

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadDocuments();
    this.loadChatFromStorage();
  }

  loadDocuments(): void {
    this.http.get<UploadedDocument[]>(`${this.apiUrl}/documents`).subscribe({
      next: (docs) => {
        this.documents = docs;
      },
      error: () => {}
    });
  }

  private loadChatFromStorage(): void {
    const saved = localStorage.getItem('chatMessages');
    if (saved) {
      try {
        this.chatMessages = JSON.parse(saved);
      } catch {
        this.chatMessages = [];
      }
    }
  }

  private saveChatToStorage(): void {
    localStorage.setItem('chatMessages', JSON.stringify(this.chatMessages));
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragging = true;
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.isDragging = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragging = false;
    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      this.uploadFile(event.dataTransfer.files[0]);
    }
  }

  onFileSelected(event: any) {
    if (event.target.files && event.target.files.length > 0) {
      this.uploadFile(event.target.files[0]);
    }
  }

  uploadFile(file: File) {
    if (file.type !== 'application/pdf' && file.type !== 'text/plain') {
      alert('Only PDF and TXT files are supported.');
      return;
    }
    this.uploading = true;
    this.uploadSuccess = false;
    const formData = new FormData();
    formData.append('file', file);

    this.http.post(`${this.apiUrl}/documents/upload`, formData).subscribe({
      next: () => {
        this.uploading = false;
        this.uploadSuccess = true;
        this.loadDocuments();
        setTimeout(() => { this.uploadSuccess = false; }, 3000);
      },
      error: (err) => {
        console.error(err);
        this.uploading = false;
        alert('Upload failed. Please try again.');
      }
    });
  }

  clearDocuments() {
    if (!confirm('Are you sure you want to clear all documents and vectors?')) return;
    this.http.delete(`${this.apiUrl}/documents/clear`).subscribe({
      next: () => {
        this.documents = [];
        // Chat history is intentionally preserved
      },
      error: (err) => {
        console.error(err);
        alert('Failed to clear documents.');
      }
    });
  }

  clearChat() {
    this.chatMessages = [];
    this.saveChatToStorage();
  }

  sendMessage() {
    if (!this.currentQuery.trim()) return;

    const query = this.currentQuery;
    this.chatMessages.push({ role: 'user', content: query });
    this.currentQuery = '';
    this.isTyping = true;
    this.saveChatToStorage();
    this.scrollToBottom();

    this.http.post<{response: string, sources: string[]}>(`${this.apiUrl}/chat`, { query }).subscribe({
      next: (res) => {
        this.isTyping = false;
        this.chatMessages.push({
          role: 'assistant',
          content: res.response,
          sources: res.sources
        });
        this.saveChatToStorage();
        this.scrollToBottom();
      },
      error: () => {
        this.isTyping = false;
        this.chatMessages.push({
          role: 'assistant',
          content: 'Sorry, I encountered an error. The local AI model may need more time — please try again.'
        });
        this.saveChatToStorage();
        this.scrollToBottom();
      }
    });
  }

  handleKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric'
    });
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      try {
        this.chatContainer.nativeElement.scrollTop = this.chatContainer.nativeElement.scrollHeight;
      } catch(err) {}
    }, 100);
  }
}

