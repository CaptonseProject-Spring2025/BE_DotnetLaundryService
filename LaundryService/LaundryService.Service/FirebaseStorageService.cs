using Google.Cloud.Firestore;
using LaundryService.Domain.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service
{
    public class FirebaseStorageService : IFirebaseStorageService
    {
        private readonly FirestoreDb _firestore;

        public FirebaseStorageService()
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "notification-firebase-adminsdk.json");
            _firestore = FirestoreDb.Create("notification-8a9a5");
        }

        public async Task SaveTokenAsync(string userId, string fcmToken)
        {
            try
            {
                var deviceId = Guid.NewGuid().ToString();

                DocumentReference docRef = _firestore.Collection("user_tokens").Document(userId)
                                                    .Collection("tokens").Document(deviceId);

                await docRef.SetAsync(new
                {
                    Token = fcmToken,
                    CreatedAt = DateTime.UtcNow
                });

                Console.WriteLine("FCMToken saved successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving fcntoken: {ex.Message}");
            }
        }

        public async Task<string?> GetUserFcmTokenAsync(string userId)
        {
            try
            {
                var tokensCollection = _firestore.Collection("user_tokens").Document(userId).Collection("tokens");
                var snapshot = await tokensCollection.Limit(1).GetSnapshotAsync();

                if (!snapshot.Documents.Any()) return null;

                var tokenData = snapshot.Documents.First().ToDictionary();
                return tokenData.ContainsKey("Token") ? tokenData["Token"].ToString() : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching FCM token: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeleteTokenAsync(string userId, string fcmToken)
        {
            try
            {
                CollectionReference tokensRef = _firestore.Collection("user_tokens").Document(userId).Collection("tokens");
                QuerySnapshot snapshot = await tokensRef.GetSnapshotAsync();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (doc.Exists && doc.ContainsField("Token") && doc.GetValue<string>("Token") == fcmToken)
                    {
                        await doc.Reference.DeleteAsync();
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting token: {ex.Message}");
                return false;
            }
        }
    }
}
