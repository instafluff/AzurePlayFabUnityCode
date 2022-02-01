using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Networking.Transport;

public class Server : MonoBehaviour
{
    public bool RunLocal;
    public NetworkDriver networkDriver;
    private NativeList<NetworkConnection> connections;
    const int numEnemies = 12; // Total number of enemies
    private byte[] enemyStatus;
    private int numPlayers = 0;

    void StartServer()
    {
        Debug.Log( "Starting Server" );

        // Start transport server
        networkDriver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = 7777;
        if( networkDriver.Bind( endpoint ) != 0 )
        {
            Debug.Log( "Failed to bind to port " + endpoint.Port );
        }
        else
        {
            networkDriver.Listen();
        }

        connections = new NativeList<NetworkConnection>( 16, Allocator.Persistent );

        enemyStatus = new byte[ numEnemies ];
        for( int i = 0; i < numEnemies; i++ )
        {
            enemyStatus[ i ] = 1;
        }
    }
    void OnDestroy()
    {
        networkDriver.Dispose();
        connections.Dispose();
    }

    // Start is called before the first frame update
    void Start()
    {
        if( RunLocal )
        {
            StartServer(); // Run the server locally
        }
        else
        {
            // TODO: Start from PlayFab configuration
        }
    }

    // Update is called once per frame
    void Update()
    {
        networkDriver.ScheduleUpdate().Complete();

        // Clean up connections
        for( int i = 0; i < connections.Length; i++ )
        {
            if( !connections[ i ].IsCreated )
            {
                connections.RemoveAtSwapBack( i );
                --i;
            }
        }

        // Accept new connections
        NetworkConnection c;
        while( ( c = networkDriver.Accept() ) != default( NetworkConnection ) )
        {
            connections.Add( c );
            Debug.Log( "Accepted a connection" );
            numPlayers++;
        }

        DataStreamReader stream;
        for( int i = 0; i < connections.Length; i++ )
        {
            if( !connections[ i ].IsCreated )
            {
                continue;
            }
            NetworkEvent.Type cmd;
            while( ( cmd = networkDriver.PopEventForConnection( connections[ i ], out stream ) ) != NetworkEvent.Type.Empty )
            {
                if( cmd == NetworkEvent.Type.Data )
                {
                    uint number = stream.ReadUInt();
                    if( number == numEnemies ) // Check that the number of enemies match
                    {
                        for( int b = 0; b < numEnemies; b++ )
                        {
                            byte isAlive = stream.ReadByte();
                            if( isAlive == 0 && enemyStatus[ b ] > 0 )
                            {
                                Debug.Log( "Enemy " + b + " destroyed by Player " + i );
                                enemyStatus[ b ] = 0;
                            }
                        }
                    }
                }
                else if( cmd == NetworkEvent.Type.Disconnect )
                {
                    Debug.Log( "Client disconnected from server" );
                    connections[ i ] = default( NetworkConnection );
                    numPlayers--;
                }
            }

            // Broadcast Game State
            networkDriver.BeginSend( NetworkPipeline.Null, connections[ i ], out var writer );
            writer.WriteUInt( numEnemies );
            for( int b = 0; b < numEnemies; b++ )
            {
                writer.WriteByte( enemyStatus[ b ] );
            }
            networkDriver.EndSend( writer );
        }
    }
}
