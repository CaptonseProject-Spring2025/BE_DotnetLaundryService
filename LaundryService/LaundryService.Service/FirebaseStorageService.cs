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
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "notification-laundry-firebase-adminsdk.json");
            _firestore = FirestoreDb.Create("notification-laundry-f73e8");
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

        public async Task<List<string>> GetUserFcmTokensAsync(string userId)
        {
            try
            {
                var tokensCollection = _firestore.Collection("user_tokens").Document(userId).Collection("tokens");
                var snapshot = await tokensCollection.GetSnapshotAsync();

                var tokens = new List<string>();
                foreach (var doc in snapshot.Documents)
                {
                    var tokenData = doc.ToDictionary();
                    if (tokenData.ContainsKey("Token"))
                    {
                        tokens.Add(tokenData["Token"].ToString());
                    }
                }

                return tokens;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching FCM tokens: {ex.Message}");
                return new List<string>();
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
