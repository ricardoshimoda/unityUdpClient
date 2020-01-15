using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;

public class NetworkMan : MonoBehaviour
{
    public UdpClient udp;
    private float xStep = 1.5f;

    private GameObject rotatingCubePrefab;
    // Start is called before the first frame update
    void Start()
    {
        rotatingCubePrefab = Resources.Load("MyRotatingCube", typeof(GameObject)) as GameObject;
        udp = new UdpClient();
        udp.Connect("34.201.53.13",12345);
        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
        udp.Send(sendBytes, sendBytes.Length);
        udp.BeginReceive(new AsyncCallback(OnReceived), udp);
        InvokeRepeating("HeartBeat", 1, 1);
    }

    void OnDestroy(){
        udp.Dispose();
    }

    public enum commands{
        NEW_CLIENT,
        UPDATE,
        OTHERS,
        DELETE
    };
    
    [Serializable]
    public class Message{
        public commands cmd;
        public Player[] players;
    }

    public Queue<Message> spawnMessages = new Queue<Message>();
    public Queue<Message> updateMessages = new Queue<Message>();
    public Queue<Message> deleteMessages = new Queue<Message>();

    [Serializable]
    public class receivedColor{
        public float R;
        public float G;
        public float B;
    }
    
    [Serializable]
    public class Player{
        public string id;
        public receivedColor color;        
    }

    public Dictionary<string, GameObject> networkedPlayers = new Dictionary<string, GameObject>();

    [Serializable]
    public class GameState{
        public Player[] players;
    }

    public Message latestMessage;
    public GameState lastestGameState;
    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);
        Debug.Log("***********************************");        
        Debug.Log(returnData);        
        latestMessage = JsonUtility.FromJson<Message>(returnData);

        try{
            switch(latestMessage.cmd){
                case commands.NEW_CLIENT:
                    spawnMessages.Enqueue(latestMessage);
                    break;
                case commands.UPDATE:
                    updateMessages.Enqueue(latestMessage);
                    break;
                case commands.OTHERS:
                    spawnMessages.Enqueue(latestMessage);
                    break;
                case commands.DELETE:
                    deleteMessages.Enqueue(latestMessage);
                    break;
                default:
                    Debug.Log("Error - no suitable message found!!!!!");
                    break;
            }
        }
        catch (Exception e){
            Debug.Log(e.ToString());
        }
        
        // schedule the next receive operation once reading is done:
        socket.BeginReceive(new AsyncCallback(OnReceived), socket);
    }

    void SpawnPlayers(){
        while(spawnMessages.Count > 0){
            var spawnMessage = spawnMessages.Dequeue();
            for(int insertPlayerCounter = 0; insertPlayerCounter < spawnMessage.players.Length; insertPlayerCounter++){
                int finalCount = networkedPlayers.Count + 1;
                float xCoord = ((int)finalCount/2) * xStep * (finalCount%2 == 1?-1:1);
                GameObject newCube = Instantiate(
                    rotatingCubePrefab,
                    new Vector3(xCoord, 0, 0), 
                    Quaternion.Euler(0, 0, 0)) as GameObject;
                newCube.GetComponent<NetworkCube>()
                    .ChangeColor(spawnMessage.players[insertPlayerCounter].color.R, spawnMessage.players[insertPlayerCounter].color.G, spawnMessage.players[insertPlayerCounter].color.B);
                networkedPlayers.Add(spawnMessage.players[insertPlayerCounter].id, newCube);
            }
        }
    }

    void UpdatePlayers(){
        while(updateMessages.Count > 0){
            var updateMessage = updateMessages.Dequeue();
            for(int updatePlayerCounter = 0; updatePlayerCounter < updateMessage.players.Length; updatePlayerCounter++){
                var cubeId = updateMessage.players[updatePlayerCounter].id;
                if(networkedPlayers.ContainsKey(cubeId)){
                    networkedPlayers[cubeId].GetComponent<NetworkCube>()
                    .ChangeColor(updateMessage.players[updatePlayerCounter].color.R, updateMessage.players[updatePlayerCounter].color.G, updateMessage.players[updatePlayerCounter].color.B);
                }
            }
        }
    }

    void DestroyPlayers(){
        while(deleteMessages.Count > 0){
            var deleteMessage = deleteMessages.Dequeue();
            for(int updatePlayerCounter = 0; updatePlayerCounter < deleteMessage.players.Length; updatePlayerCounter++){
                var cubeId = deleteMessage.players[updatePlayerCounter].id;
                if(networkedPlayers.ContainsKey(cubeId)){
                    Destroy(networkedPlayers[cubeId]);
                    networkedPlayers.Remove(cubeId);
                }
            }
        }

    }
    
    void HeartBeat(){
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);
    }

    void Update(){
        SpawnPlayers();
        UpdatePlayers();
        DestroyPlayers();
    }
}