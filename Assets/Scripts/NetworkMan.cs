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

    private bool newPlayer = true;

    private string mainId = string.Empty;

    private GameObject rotatingCubePrefab;
    // Start is called before the first frame update
    void Start()
    {
        rotatingCubePrefab = Resources.Load("MyRotatingCube", typeof(GameObject)) as GameObject;
        udp = new UdpClient();
        udp.Connect("54.172.73.176", 12345);
        Debug.Log(((IPEndPoint)udp.Client.LocalEndPoint).Port.ToString());
        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
        udp.Send(sendBytes, sendBytes.Length);
        udp.BeginReceive(new AsyncCallback(OnReceived), udp);
        InvokeRepeating("HeartBeat", 1, 1);        
        InvokeRepeating("SendPosition", 1.0f/30.0f, 1);        
    }

    void OnDestroy(){
        udp.Close();
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
    public object lockSpawn = new object();
    public Queue<Message> updateMessages = new Queue<Message>();
    public object lockUpdate= new object();
    public Queue<Message> deleteMessages = new Queue<Message>();
    public object lockDelete= new object();

    [Serializable]
    public class receivedColor{
        public float R;
        public float G;
        public float B;
    }
    
    [Serializable]
    public class serverPosition{
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class Player{
        public string id;
        public receivedColor color;    
        public serverPosition position;    
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
                    lock(lockSpawn){
                        spawnMessages.Enqueue(latestMessage);
                    }
                    break;
                case commands.UPDATE:
                    lock(lockUpdate)
                    {
                        updateMessages.Enqueue(latestMessage);
                    }
                    break;
                case commands.OTHERS:
                    lock(lockSpawn){
                        spawnMessages.Enqueue(latestMessage);
                    }
                    break;
                case commands.DELETE:
                    lock(lockDelete){
                        deleteMessages.Enqueue(latestMessage);
                    }
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
        lock(lockSpawn)
        {
            while(spawnMessages.Count > 0){
                var spawnMessage = spawnMessages.Dequeue();
                for(int insertPlayerCounter = 0; insertPlayerCounter < spawnMessage.players.Length; insertPlayerCounter++){
                    int finalCount = networkedPlayers.Count + 1;
                    GameObject newCube = Instantiate(
                        rotatingCubePrefab,
                        new Vector3(
                            spawnMessage.players[insertPlayerCounter].position.x,
                            spawnMessage.players[insertPlayerCounter].position.y,
                            spawnMessage.players[insertPlayerCounter].position.z
                        ), 
                        Quaternion.Euler(0, 0, 0)) as GameObject;
                    NetworkCube thisCubeHere = newCube.GetComponent<NetworkCube>();
                    thisCubeHere.id = spawnMessage.players[insertPlayerCounter].id;
                    if((insertPlayerCounter == spawnMessage.players.Length - 1) && newPlayer){
                        newPlayer = false;
                        thisCubeHere.mainCube = true;
                        mainId = thisCubeHere.id;
                    }
                    thisCubeHere.ChangeColor(spawnMessage.players[insertPlayerCounter].color.R, spawnMessage.players[insertPlayerCounter].color.G, spawnMessage.players[insertPlayerCounter].color.B);
                    networkedPlayers.Add(spawnMessage.players[insertPlayerCounter].id, newCube);
                }
            }
        }
    }

    void UpdatePlayers(){
        lock(lockUpdate){
            while(updateMessages.Count > 0){
                var updateMessage = updateMessages.Dequeue();
                for(int updatePlayerCounter = 0; updatePlayerCounter < updateMessage.players.Length; updatePlayerCounter++){
                    var cubeId = updateMessage.players[updatePlayerCounter].id;
                    if(networkedPlayers.ContainsKey(cubeId)){
                        var currentCube = networkedPlayers[cubeId];
                        /*
                        currentCube.GetComponent<NetworkCube>()
                        .ChangeColor(updateMessage.players[updatePlayerCounter].color.R, 
                                     updateMessage.players[updatePlayerCounter].color.G, 
                                     updateMessage.players[updatePlayerCounter].color.B);*/
                        //if(cubeId != mainId){
                        currentCube.transform.position = new Vector3(
                            updateMessage.players[updatePlayerCounter].position.x,
                            updateMessage.players[updatePlayerCounter].position.y,
                            updateMessage.players[updatePlayerCounter].position.z
                        );
                        //}
                    }
                }
            }
        }
    }

    float mainX = 0;
    float mainY = 0;
    void RecordPosition()
    {
        if(!string.IsNullOrWhiteSpace(mainId)){
            mainX = networkedPlayers[mainId].GetComponent<NetworkCube>().calculatedPosition.x;
            mainY = networkedPlayers[mainId].GetComponent<NetworkCube>().calculatedPosition.y;
            string positionMessage = "{\"op\":\"cube_position\", \"position\":{\"x\":" + mainX + ", \"y\":" + mainY + ",\"z\":0}}";
            Debug.Log("Sending Position Message: " + positionMessage);
            Byte[] sendBytes = Encoding.ASCII.GetBytes(positionMessage);
            udp.Send(sendBytes, sendBytes.Length);
        }
    }

    void SendPosition(){
        if(!string.IsNullOrWhiteSpace(mainId)){
            string positionMessage = "{\"op\":\"cube_position\", \"position\":{\"x\":" + mainX + ", \"y\":" + mainY + ",\"z\":0}}";
            Debug.Log("Sending Position Message: " + positionMessage);
            Byte[] sendBytes = Encoding.ASCII.GetBytes(positionMessage);
            udp.Send(sendBytes, sendBytes.Length);
        }
    }

    void DestroyPlayers(){
        lock(lockDelete)
        {
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
    }
    
    void HeartBeat(){
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);
    }

    void Update(){
        SpawnPlayers();
        RecordPosition();
        UpdatePlayers();
        DestroyPlayers();
    }
}