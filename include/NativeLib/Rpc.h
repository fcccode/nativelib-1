#pragma once

#include <unordered_map>
#include <stack>
#include <string>

#include <NativeLib/Json.h>
#include <NativeLib/Exceptions.h>

namespace nl
{
    namespace rpc
    {
        DeclarePassthroughException(RpcException);

        typedef void(*pfn)(class Server* rpc, const nl::JsonObject* request, nl::JsonObject* response);

        class Server
        {
        public:
            Server();
            ~Server();
            void Run(const wchar_t* pipeName);

            void Bind(const std::string& name, pfn procedure)
            {
                m_procedures.insert(std::make_pair(name, procedure));
            }

            LONG_PTR GetUserData() { return m_lUserData; }
            void SetTag(LONG_PTR lUserData) { m_lUserData = lUserData; }

        protected:
            void HandleRequest(class PipeClient* client, class DataBuffer& buffer, int requestId);
            void SendError(class PipeClient* client, int requestId, const char* message);
            void SendJson(class PipeClient* client, int requestId, const std::unique_ptr<nl::JsonObject>& json);

        private:
            std::unordered_map<std::string, pfn> m_procedures;
            HANDLE m_hIocp;
            LONG_PTR m_lUserData;

            void ConnectNewClient(const wchar_t* pipeName);
        };
    }
}