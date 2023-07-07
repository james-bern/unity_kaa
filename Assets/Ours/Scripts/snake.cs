using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Rendering;

using Unity.Mathematics;
using static Unity.Mathematics.math;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
unsafe public class snake : MonoBehaviour {

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    static public extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32")]
    static public extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

    [DllImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static public extern bool FreeLibrary(IntPtr hModule);

    IntPtr library;

    delegate void cpp_init();
    delegate int cpp_getNumVertices();
    delegate int cpp_getNumTriangles();
    delegate void cpp_reset();

    delegate void cpp_solve(
        int num_feature_points,
        void *_targetEnabled__BOOL_ARRAY,
        void *_targetPositions__FLOAT3_ARRAY, //node pos
        void *vertex_positions__FLOAT3_ARRAY,
        void *vertex_normals__FLOAT3_ARRAY,
        void *triangle_indices__UINT_ARRAY,
        void *feature_point_positions__FLOAT3__ARRAY); // pos on snake
    
    delegate bool cpp_castRay(
        float ray_origin_x,
        float ray_origin_y,
        float ray_origin_z,
        float ray_direction_x,
        float ray_direction_y,
        float ray_direction_z,
        void *intersection_position__FLOAT_ARRAY__LENGTH_3,
        bool pleaseSetFeaturePoint,
        int indexOfFeaturePointToSet,
        void *feature_point_positions__FLOAT3__ARRAY = null);

    cpp_init init;
    cpp_getNumVertices getNumVertices;
    cpp_getNumTriangles getNumTriangles;
    cpp_reset reset;
    cpp_solve solve;
    cpp_castRay castRay;

    void LoadDLL() {
        library = LoadLibrary("Assets/snake");
        init = (cpp_init) Marshal.GetDelegateForFunctionPointer(GetProcAddress(library, "cpp_init"), typeof(cpp_init));
        getNumVertices = (cpp_getNumVertices) Marshal.GetDelegateForFunctionPointer(GetProcAddress(library, "cpp_getNumVertices"), typeof(cpp_getNumVertices));
        getNumTriangles = (cpp_getNumTriangles) Marshal.GetDelegateForFunctionPointer(GetProcAddress(library, "cpp_getNumTriangles"), typeof(cpp_getNumTriangles));
        reset = (cpp_reset) Marshal.GetDelegateForFunctionPointer(GetProcAddress(library, "cpp_reset"), typeof(cpp_reset));
        solve = (cpp_solve) Marshal.GetDelegateForFunctionPointer(GetProcAddress(library, "cpp_solve"), typeof(cpp_solve));
        castRay = (cpp_castRay) Marshal.GetDelegateForFunctionPointer(GetProcAddress(library, "cpp_castRay"), typeof(cpp_castRay));
    }


    public GameObject node_1;
    public GameObject head;
    public GameObject targetPrefab;
    public GameObject leftHand;
    public GameObject rightHand;
    public GameObject interactionDotRight;
    public GameObject interactionDotLeft;
    public GameObject[] targets;
    public NodeManager nodeManager;
    public bool isUpdating = true;
    public Vector3 restPos; 
    public UnityEngine.XR.InputFeatureUsage<float> fl;

    bool usable;
    Vector3 node_pos;

    NativeArray<float> intersection_position;
    NativeArray<float3> posOnSnake;


    void Awake () {
        LoadDLL();
        init();
        intersection_position = new NativeArray<float>(3, Allocator.Persistent);
        posOnSnake = new NativeArray<float3>(nodeManager.numNodes, Allocator.Persistent);
        targets = new GameObject[nodeManager.numNodes];
        targets[0] = head;
        usable = true;

        Update(); 
        isUpdating = false;
    }

    void Update () {


        bool leftHandFire, rightHandFire, leftHandReset, rightHandReset; {
            bool value;
            float valuef;
            leftHandFire    = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand ).TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out valuef) && valuef >= 0.1f;
            rightHandFire   = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out valuef) && valuef >= 0.1f;
            leftHandReset   = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand ).TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out value) && value;
            rightHandReset  = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out value) && value;
        }
        /*
        int leftTargetIndex, rightTargetIndex; {
            leftTargetIndex  = -1;
            rightTargetIndex = -1;
            foreach (GameObject node in nodeManager.nodes) {

            }
            // TODO (Option B): Do it yourself.
            // TODO (Option C): Work with what unity does give us. :(
            // TODO (Option A): Figure out how to get the data that Unity clearly already has out of Unity.
        }
        */

        //mesh gen
        if (isUpdating) {

            node_pos = node_1.transform.position; // ???

            int triangleIndexCount = getNumTriangles() * 3; 

            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];
            { // TODO: link to whatever tutorial/docs we got this stuff from
                int vertexCount = getNumVertices();
                int vertexAttributeCount = 2;

                var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(vertexAttributeCount, Allocator.Temp);
                vertexAttributes[0] = new VertexAttributeDescriptor(dimension: 3); // position?
                vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3, stream: 1);
                meshData.SetVertexBufferParams(vertexCount, vertexAttributes);
                vertexAttributes.Dispose();
  
                meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt16);
            }


            NativeArray<int> nativeBools = new NativeArray<int>(nodeManager.numNodes, Allocator.Temp);
            NativeArray<float3> nativeTargetPos = new NativeArray<float3>(nodeManager.numNodes, Allocator.Temp);
            {
                bool[] bools = nodeManager.getBools();
                for(int k = 0; k < nodeManager.numNodes; k++){
                    if(bools[k]) nativeBools[k] = 1;
                    else nativeBools[k] = 0;
                    nativeTargetPos[k] = new float3 (nodeManager.nodes[k].transform.position.x, nodeManager.nodes[k].transform.position.y, nodeManager.nodes[k].transform.position.z);
                }
            }
            solve(
                nodeManager.numNodes,
                NativeArrayUnsafeUtility.GetUnsafePtr(nativeBools),
                NativeArrayUnsafeUtility.GetUnsafePtr(nativeTargetPos),
                NativeArrayUnsafeUtility.GetUnsafePtr(meshData.GetVertexData<float3>(0)),
                NativeArrayUnsafeUtility.GetUnsafePtr(meshData.GetVertexData<float3>(1)),
                NativeArrayUnsafeUtility.GetUnsafePtr(meshData.GetIndexData<ushort>()),
                NativeArrayUnsafeUtility.GetUnsafePtr(posOnSnake));
            nativeBools.Dispose();
            nativeTargetPos.Dispose();



            //head.transform.position = posOnSnake[0];
            for(int k = 0; k < nodeManager.nextAvalible; k++){
                targets[k].transform.position = posOnSnake[k];
            }


            // FORNOW: TODO: try moving this before scope if we ever start passing triangle indices only once (e.g., in Awake)
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount));

            Mesh mesh = new Mesh {
                name = "Procedural Mesh"
            };
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().mesh = mesh;
        }



        //left cast
        Vector3 ray_origin = new Vector3(0.0f, 0.0f, 0.0f);
        foreach(Transform child in leftHand.transform){
            if(child.name == "[Ray Interactor] Ray Origin") ray_origin = child.position;
        } 

        Vector3 ray_direction = new Vector3(1.0f, 1.0f, 1.0f);
        ray_direction =  leftHand.transform.rotation * Vector3.forward;



        if (castRay(
        ray_origin.x,ray_origin.y,ray_origin.z,
        ray_direction.x,ray_direction.y,ray_direction.z,
        NativeArrayUnsafeUtility.GetUnsafePtr(intersection_position),
        false, -1)) {
            interactionDotLeft.SetActive(true);
            interactionDotLeft.transform.position = new Vector3(intersection_position[0], intersection_position[1], intersection_position[2]);
        }
        else{
            interactionDotLeft.SetActive(false);
        }

                //return;

        //right cast
        foreach(Transform child in rightHand.transform){
            if(child.name == "[Ray Interactor] Ray Origin") ray_origin = child.position;
        } 
        ray_direction =  rightHand.transform.rotation * Vector3.forward;
        
        if (castRay(
        ray_origin.x,ray_origin.y,ray_origin.z,
        ray_direction.x,ray_direction.y,ray_direction.z,
        NativeArrayUnsafeUtility.GetUnsafePtr(intersection_position),
        false, -1)) {
            interactionDotRight.SetActive(true);
            interactionDotRight.transform.position = new Vector3(intersection_position[0], intersection_position[1], intersection_position[2]);
        }
        else{
            interactionDotRight.SetActive(false);
        }

        {
            //generate new target node
            if(rightHandFire || leftHandFire)
            {
                if (usable) GenNode(leftHandFire, rightHandFire);
                usable = false;
            } //reset
            else if(rightHandReset || leftHandReset)
            {
                if (usable) {
                    usable = false;
                    nodeManager.Setup();
                    reset();
                    for(int k = 1; k < nodeManager.numNodes; k++){
                        if(targets[k] != null) {
                            Destroy(targets[k]);
                        }
                    }
                    isUpdating = true;
                    Update(); 
                    isUpdating = false;          
                    nodeManager.nodes[0].SetActive(true);
                }
                usable = false;
            }
            else usable = true;
        }
    }

    public void GenNode(bool leftHandFire, bool rightHandFire) {
        if(gameObject.activeSelf && (nodeManager.nextAvalible != nodeManager.numNodes)){
            Vector3 ray_origin_r = new Vector3(0.0f, 0.0f, 0.0f); Vector3 ray_origin_l = new Vector3(0.0f, 0.0f, 0.0f);
            if(rightHand!=null && leftHand!=null){
                foreach(Transform child in rightHand.transform){if(child.name == "[Ray Interactor] Ray Origin") ray_origin_r = child.position;} 
                foreach(Transform child in leftHand.transform){if(child.name == "[Ray Interactor] Ray Origin") ray_origin_l = child.position;}
            }
            Vector3 ray_direction_r = rightHand.transform.rotation * Vector3.forward; Vector3 ray_direction_l = leftHand.transform.rotation * Vector3.forward;

            if (castRay(ray_origin_r.x, ray_origin_r.y, ray_origin_r.z, ray_direction_r.x, ray_direction_r.y, ray_direction_r.z, NativeArrayUnsafeUtility.GetUnsafePtr(intersection_position), rightHandFire, nodeManager.nextAvalible)) {
                InstantiateNode();
            }
            if (castRay(ray_origin_l.x, ray_origin_l.y, ray_origin_l.z, ray_direction_l.x, ray_direction_l.y, ray_direction_l.z, NativeArrayUnsafeUtility.GetUnsafePtr(intersection_position), leftHandFire, nodeManager.nextAvalible)) {
                InstantiateNode();
            }
        }
    }

    void InstantiateNode() {
        Vector3 pos = new Vector3(intersection_position[0], intersection_position[1], intersection_position[2]);                
        GameObject tar = Instantiate(targetPrefab, pos, Quaternion.identity);
        targets[nodeManager.nextAvalible] = tar;
        nodeManager.SetProperties(pos);
        foreach(Transform child in nodeManager.nodes[nodeManager.nextAvalible-1].transform) {
            if(child.name == "Lines") {
                foreach(Transform child2 in child){
                    child2.GetComponent<LinePos>().head = tar;
                }
            }
        }
    }

    // public bool IsLeft() {
    //     Vector3 ray_origin_r = new Vector3(0.0f, 0.0f, 0.0f); Vector3 ray_origin_l = new Vector3(0.0f, 0.0f, 0.0f);
    //     if(rightHand!=null && leftHand!=null){
    //         foreach(Transform child in rightHand.transform){if(child.name == "[Ray Interactor] Ray Origin") ray_origin_r = child.position;} 
    //         foreach(Transform child in leftHand.transform){if(child.name == "[Ray Interactor] Ray Origin") ray_origin_l = child.position;}
    //     }
    //     Vector3 ray_direction_r = rightHand.transform.rotation * Vector3.forward; Vector3 ray_direction_l = leftHand.transform.rotation * Vector3.forward;
    //     if (castRay(ray_origin_r.x, ray_origin_r.y, ray_origin_r.z, ray_direction_r.x, ray_direction_r.y, ray_direction_r.z, NativeArrayUnsafeUtility.GetUnsafePtr(intersection_position), rightHandFire, nodeManager.nextAvalible)) {
    //             InstantiateNode();
    //         }
    // }



    void OnApplicationQuit () {
        FreeLibrary(library);
        posOnSnake.Dispose(); 
        intersection_position.Dispose();
    }
}