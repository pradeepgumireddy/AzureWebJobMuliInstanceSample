using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace PocWebJob.Utilities
{
    public class WOCommonUtility
    {
        public static XmlNodeList WoXmlNodes;
        public static XmlSchemaSet _schema;

        private static void RemoveXmlNode(XmlDocument xml, string nodeName)
        {
            WoXmlNodes = xml.SelectNodes("//WorkOrder");
            var nodeToRemove = FindNode(WoXmlNodes, nodeName);
            nodeToRemove?.ParentNode.RemoveChild(nodeToRemove);

        }
        public static XmlDocument SetNodeValue(XmlDocument xml, string nodeName, string nodeValue)
        {
            //const string nodeName = "Unified_WO_ID";
            WoXmlNodes = xml.SelectNodes("//WorkOrder");
            var node = FindNode(WoXmlNodes, nodeName);
            node.InnerText = nodeValue;
            return xml;
        }
        public static string GetNodeValue(XmlDocument xml, string nodeName)
        {
            // const string nodeName = "Unified_WO_ID";
            WoXmlNodes = xml.SelectNodes("//WorkOrder");
            var node = FindNode(WoXmlNodes, nodeName);
            return node.InnerText;
        }

        private static XmlNode FindNode(XmlNodeList list, string nodeName)
        {
            if (list.Count <= 0) return null;
            foreach (XmlNode node in list)
            {
                if (node.Name.Equals(nodeName)) return node;
                if (!node.HasChildNodes) continue;
                var nodeFound = FindNode(node.ChildNodes, nodeName);
                if (nodeFound != null)
                    return nodeFound;
            }

            return null;
        }
    }
}
