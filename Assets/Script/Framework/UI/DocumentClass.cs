﻿using System;
using System.Collections;
using System.Collections.Generic;
using Babeltime.Log;
using LuaInterface;
using UnityEditor;
using UnityEngine;
using Framework.core;

namespace Framework.UI
{
    public class DocumentClass : MonoBehaviour
    {
        [SerializeField]
        private string LuaClass = "";

        private int contextId = -1;

        [NoToLua]
        public void SetContextId(int value)
        {
            if (contextId == value)
            {
                return;
            }
            contextId = value;
            MainGame.Ins.GetPrefabLua(contextId);
            BindLuaClass();
        }

        public int GetContextId()
        {
            return contextId;
        }

        public string GetLuaClassName()
        {
            return LuaClass;
        }
        
//        该函数在调用前，必须保证当前lua栈顶有一个lua的Prefab对象。也就是说，栈不为空!
        private void BindLuaClass()
        {
            var luaState = MainGame.Ins.LuaState;
            if (luaState.LuaIsNil(-1))
            {
                BTLog.Error("该函数在调用前必须保证lua栈顶上有一个lua的Prefab对象");
                return;
            }
//            TODO 这里考虑有没有必要把这个gameObject传给lua
            luaState.PushVariant(gameObject);
            luaState.LuaSetField(-2, "gameObject");
            BindFieldsOnTrans(transform, luaState.LuaGetTop());
//            完成绑定之后，广播complete事件
            luaState.LuaGetField(-1, "DispatchMessage");
            if (luaState.LuaIsNil(-1))
            {
                luaState.LuaPop(1);
                BTLog.Warning("Prefab Lua must has Method:DispatchMessage");
                return;
            }
            luaState.LuaInsert(-2);
            luaState.Push("COMPLETE");
            luaState.LuaCall(2, 0);
        }

        private void BindFieldsOnTrans(Transform trans, int topIdx)
        {
            BTLog.Debug("BindFieldsOnTrans trans:{0}", trans.name);
            var luaState = MainGame.Ins.LuaState;
            var numChildren = trans.childCount;
            for (int i = 0; i < numChildren; i++)
            {
                var child = trans.GetChild(i);
                var childName = child.name;
                if (childName == "") continue;
                var suffix = Utils.GetSuffixOfGoName(childName);
//                如果不是合法后缀，则直接进入下一级，检测子go有没有需要绑定的
                if (!Utils.IsValidSuffix(suffix))
                {
                    BindFieldsOnTrans(child, topIdx);
                    continue;
                }
//                对Doc进行特殊处理，这个不能直接绑定cs组件，需要创建一个lua对象，然后进行绑定
                if (suffix == "_Doc")
                {
                    var childDoc = child.GetComponent<DocumentClass>();
                    childDoc.CreatePrefabAndBindLuaClass();
                    var childContextId = childDoc.GetContextId();
                    MainGame.Ins.GetPrefabLua(childContextId);
                    luaState.LuaSetField(topIdx, childName);
                }
                else
                {
                    BindFieldsOnTrans(child, topIdx);
                    var T = Utils.GetTypeByComponentSuffix(suffix);
                    if (T == null) continue;
                    luaState.PushVariant(child.GetComponent(T));
                    luaState.LuaSetField(topIdx, childName);
                }
                BTLog.Debug("bind {0}. name:{1} childName:{2}", suffix, trans.name, childName);

            }
        }
//        TODO 目前暂时确定父类一定是Framework.UI.Prefab类，不使用自定义父类，因为检测自定义父类是从Framework.UI.Prefab继承而来，比较麻烦，而且考虑使用状态而不是继承来重用Prefab
//        [SerializeField]
//        private string SuperClass = "";
        // Start is called before the first frame update
        void Start()
        {
            if (contextId == -1)
            {
                CreatePrefabAndBindLuaClass();
            }
//            BTLog.Error("documentclass contextID:{0}", contextID);
        }

//        通过在cs端创建lua的Prefab对象进行绑定
        private void CreatePrefabAndBindLuaClass()
        {
            var luaState = MainGame.Ins.LuaState;
            luaState.LuaGetGlobal("getPrefabID");
            if (luaState.LuaIsNil(-1))
            {
                luaState.LuaPop(1);
                BTLog.Error("can not find lua function getPrefabID");
                return;
            }
            
            var className = Utils.MakeClassName(LuaClass);
            luaState.LuaGetGlobal(className);
            if (luaState.LuaIsNil(-1))
            {
                luaState.LuaPop(2);
                BTLog.Error("can not find lua class:{0}", LuaClass);
                return;
            }
            luaState.LuaGetField(-1, "New");
            if (luaState.LuaIsNil(-1))
            {
                luaState.LuaPop(3);
                BTLog.Error("can not find constructor for lua class:{0}", LuaClass);
                return;
            }
            luaState.LuaCall(0, 1);
//            删除luaclass
            luaState.LuaRemove(-2);
            luaState.LuaDup();
            //将dup出来的prefab实例放到栈底备用
            luaState.LuaInsert(-3);

            //call getPrefabID获取contextId
            luaState.LuaCall(1, 1);
            contextId = luaState.LuaToInteger(-1);
            luaState.LuaPop(1);
//            var prefab = luaState.ToVariant(-1) as LuaTable;
            BindLuaClass();
        }

        // Update is called once per frame
        void Update()
        {
            
        }
        
        private void OnDestroy()
        {
            
        }
    }
}

