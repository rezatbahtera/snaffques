using LiteDB;
using System;
using System.Collections;
using System.Collections.Generic;

// This class now belongs in the SnaffCore library to be accessible by other core components.
namespace SnaffCore.Queue
{
    public class QueueManager : IDisposable
    {
        private LiteDatabase db;
        private ILiteCollection<ShareFinding> shareQueue;
        private ILiteCollection<FileFinding> fileQueue;

        public QueueManager(string dbPath = "snaffler_queue.db")
        {
            db = new LiteDatabase(dbPath);
            shareQueue = db.GetCollection<ShareFinding>("share_queue");
            fileQueue = db.GetCollection<FileFinding>("file_queue");
        }

        // Methods for the Share Discovery phase
        public void AddShareToQueue(string sharePath)
        {
            if (shareQueue.FindOne(x => x.SharePath == sharePath) == null)
            {
                shareQueue.Insert(new ShareFinding { SharePath = sharePath });
            }
        }

        public IEnumerable<ShareFinding> GetSharesToEnumerate()
        {
            return shareQueue.FindAll();
        }

        // Methods for the File Enumeration phase
        public void AddFileToQueue(string filePath, long fileSize)
        {
            if (fileQueue.FindOne(x => x.FilePath == filePath) == null)
            {
                fileQueue.Insert(new FileFinding { FilePath = filePath, FileSize = fileSize });
            }
        }

        // Method for the Scan phase
        public IEnumerable<FileFinding> GetFilesToScan()
        {
            return fileQueue.Find(Query.All());
        }

        public void MarkFileAsScanned(ObjectId id)
        {
            fileQueue.Delete(id);
        }

        public long GetFileQueueCount()
        {
            return fileQueue.LongCount();
        }

        public void Dispose()
        {
            db?.Dispose();
        }
    }

    public class ShareFinding
    {
        public ObjectId Id { get; set; }
        public string SharePath { get; set; }
    }

    public class FileFinding
    {
        public ObjectId Id { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
    }
}
