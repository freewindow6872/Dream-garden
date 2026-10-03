using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GardenerEditor
{
    public enum IssueSeverity { Info, Warning, Error }

    public class MapIssue
    {
        public IssueSeverity severity;
        public string message;
        public Object target;
        public string fixLabel;
        public System.Action fix;
    }

    /// <summary>스테이지에서 흔히 생기는 실수를 찾아준다. (에디터 전용)</summary>
    public static class MapEditorValidator
    {
        public static List<MapIssue> Run(GameObject stage)
        {
            List<MapIssue> list = new List<MapIssue>();

            Transform platforms = MapEditorUtil.GetGroup(stage, MapNames.Platforms, false);
            Transform start = MapEditorUtil.FindMarker(stage, MapNames.PlayerStart);
            Transform goal = MapEditorUtil.FindMarker(stage, MapNames.Goal);

            List<Rect> solidRects = new List<Rect>();
            List<Rect> allRects = new List<Rect>();

            // ---------------------------------------------------- 발판
            if (platforms == null || platforms.childCount == 0)
            {
                Add(list, IssueSeverity.Warning, "발판이 하나도 없습니다.", stage, null, null);
            }
            else
            {
                int groundLayer = GameLayers.Ground;
                List<Rect> checkedRects = new List<Rect>();

                for (int i = 0; i < platforms.childCount; i++)
                {
                    Transform t = platforms.GetChild(i);
                    GameObject go = t.gameObject;
                    Collider2D col = go.GetComponent<Collider2D>();

                    if (col == null)
                    {
                        Add(list, IssueSeverity.Error,
                            go.name + ": 콜라이더가 없어서 플레이어가 통과합니다.", go,
                            "BoxCollider2D 추가", delegate
                            {
                                BoxCollider2D b = Undo.AddComponent<BoxCollider2D>(go);
                                b.size = Vector2.one;
                            });
                    }
                    else if (col.isTrigger)
                    {
                        Collider2D c = col;
                        Add(list, IssueSeverity.Warning,
                            go.name + ": 콜라이더가 Trigger 라서 플레이어가 통과합니다.", go,
                            "Trigger 해제", delegate
                            {
                                Undo.RecordObject(c, "Fix Trigger");
                                c.isTrigger = false;
                            });
                    }

                    if (go.layer != groundLayer)
                    {
                        Add(list, IssueSeverity.Error,
                            go.name + ": Ground 레이어가 아니라서 바닥으로 인식되지 않습니다. (점프 불가)", go,
                            "Ground 레이어로 변경", delegate
                            {
                                Undo.RecordObject(go, "Fix Layer");
                                go.layer = groundLayer;
                            });
                    }

                    Rect r = MapEditorUtil.GetRect(t);
                    allRects.Add(r);
                    if (!MapEditorUtil.IsOneWay(t)) solidRects.Add(r);

                    // 완전히 같은 위치에 겹친 발판
                    for (int k = 0; k < checkedRects.Count; k++)
                    {
                        Rect o = checkedRects[k];
                        if (Mathf.Abs(o.xMin - r.xMin) < 0.001f && Mathf.Abs(o.xMax - r.xMax) < 0.001f &&
                            Mathf.Abs(o.yMin - r.yMin) < 0.001f && Mathf.Abs(o.yMax - r.yMax) < 0.001f)
                        {
                            Add(list, IssueSeverity.Warning,
                                go.name + ": 같은 위치에 발판이 겹쳐 있습니다.", go,
                                "이 발판 삭제", delegate
                                {
                                    Scene sc = go.scene;
                                    Undo.DestroyObjectImmediate(go);
                                    EditorSceneManager.MarkSceneDirty(sc);
                                });
                            break;
                        }
                    }
                    checkedRects.Add(r);
                }
            }

            // ---------------------------------------------------- 시작점
            if (start == null)
            {
                Add(list, IssueSeverity.Error, "시작점(PlayerStart)이 없습니다. '시작점' 도구로 배치하세요.", stage, null, null);
            }
            else
            {
                Vector2 sp = start.position;
                Rect body = new Rect(sp.x - 0.49f, sp.y - 0.49f, 0.98f, 0.98f);

                for (int i = 0; i < solidRects.Count; i++)
                {
                    if (body.Overlaps(solidRects[i]))
                    {
                        Add(list, IssueSeverity.Error, "시작점이 발판과 겹쳐 있습니다. (플레이어가 끼입니다)", start.gameObject, null, null);
                        break;
                    }
                }

                bool hasFloor = false;
                for (int i = 0; i < allRects.Count; i++)
                {
                    Rect r = allRects[i];
                    if (r.xMax > body.xMin && r.xMin < body.xMax && r.yMax <= sp.y - 0.45f)
                    {
                        hasFloor = true;
                        break;
                    }
                }
                if (!hasFloor)
                    Add(list, IssueSeverity.Warning, "시작점 아래에 발판이 없습니다. (시작하자마자 추락)", start.gameObject, null, null);
            }

            if (goal == null)
                Add(list, IssueSeverity.Warning, "골(Goal)이 없습니다.", stage, null, null);

            // ---------------------------------------------------- 플레이어 / 카메라
            PlayerController2D pc = Object.FindObjectOfType<PlayerController2D>();
            if (pc == null)
            {
                Add(list, IssueSeverity.Warning, "씬에 플레이어(PlayerController2D)가 없습니다.", null, null, null);
            }
            else
            {
                Transform startRef = start;

                if (startRef != null && Vector2.Distance(pc.transform.position, startRef.position) > 0.05f)
                {
                    Add(list, IssueSeverity.Info, "플레이어가 시작점과 다른 위치에 있습니다.", pc.gameObject,
                        "플레이어·카메라를 시작점으로", delegate { MapEditorUtil.AlignPlayerAndCameraToStart(stage); });
                }

                PlayerRespawn pr = pc.GetComponent<PlayerRespawn>();
                if (pr != null)
                {
                    SerializedProperty rp = new SerializedObject(pr).FindProperty("respawnPoint");
                    if (rp != null && rp.objectReferenceValue == null)
                    {
                        System.Action fixRespawn = null;
                        if (startRef != null)
                            fixRespawn = delegate { MapEditorUtil.LinkRespawn(startRef); };

                        Add(list, IssueSeverity.Warning, "PlayerRespawn의 리스폰 지점이 비어 있습니다. (추락해도 복귀하지 않음)", pr.gameObject,
                            fixRespawn != null ? "시작점으로 연결" : null, fixRespawn);
                    }
                }

                Camera cam = Camera.main;
                CameraFollow2D follow = cam != null ? cam.GetComponent<CameraFollow2D>() : null;
                if (cam == null)
                {
                    Add(list, IssueSeverity.Warning, "MainCamera 태그의 카메라가 없습니다.", null, null, null);
                }
                else if (follow == null)
                {
                    Add(list, IssueSeverity.Warning, "카메라에 CameraFollow2D가 없습니다.", cam.gameObject, null, null);
                }
                else
                {
                    SerializedProperty tp = new SerializedObject(follow).FindProperty("target");
                    if (tp != null && tp.objectReferenceValue == null)
                    {
                        CameraFollow2D f = follow;
                        Add(list, IssueSeverity.Warning, "카메라의 Target이 비어 있습니다.", cam.gameObject,
                            "플레이어로 연결", delegate
                            {
                                SerializedObject so = new SerializedObject(f);
                                so.FindProperty("target").objectReferenceValue = pc.transform;
                                so.ApplyModifiedProperties();
                            });
                    }
                }
            }

            return list;
        }

        private static void Add(List<MapIssue> list, IssueSeverity severity, string message, Object target, string fixLabel, System.Action fix)
        {
            MapIssue issue = new MapIssue();
            issue.severity = severity;
            issue.message = message;
            issue.target = target;
            issue.fixLabel = fixLabel;
            issue.fix = fix;
            list.Add(issue);
        }
    }
}
